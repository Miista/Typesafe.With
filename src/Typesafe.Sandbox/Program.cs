using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using Mono.Reflection;
using Typesafe.With;

namespace Typesafe.Sandbox
{
    interface IPerson
    {
        string Name { get; set; }
    }

    interface IStudent : IPerson
    {
        House House { get; set; }
    }

    class HogwartsStudents : IStudent
    {
        public string X;
        public string Name { get; set; }
        public House House { get; set; }

        public override string ToString() => $"Name={Name};House={House}";
    }
    
    class Person
    {
        public string Name { get; }
        public int Age { get; }
        public string LastName { get; set; }

        public Person(string name, int age)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Age = age;
        }

        public override string ToString() => $"Name={Name}; Age={Age}; LastName={LastName}; HashCode={GetHashCode()};";
    }

    class NoCtor
    {
        public string Name { get; set; }

        public override string ToString() => $"Name={Name};";
    }

    class UnrelatedType
    {
    }
    
    class Student
    {
        public string Name { get; }
        public House House { get; }
    
        public Student(string name, House house) => (Name, House) = (name, house);
    }

    enum House
    {
        Gryffindor,
        Slytherin
    }

    struct Replacement
    {
        public PropertyInfo Property { get; }
        public object Value { get; }
        public Stack<MemberInfo> Path { get; }

        public bool IsFactory
        {
            get
            {
                var type = Value.GetType();
                
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Func<,>))
                {
                    var genericArguments = type.GetGenericArguments();
                    return genericArguments[0] == Property.PropertyType && genericArguments[1] == Property.PropertyType;
                }

                return false;
            }
        }
        
        public bool IsNested => Path?.Count > 0;
        public MemberInfo Member => IsNested ? Path.Peek() : null;

        public Replacement(PropertyInfo property, object value, Stack<MemberInfo> path)
        {
            Property = property ?? throw new ArgumentNullException(nameof(property));
            Value = value;
            Path = path;
        }

        public Replacement Lift()
        {
            Path.Pop();
            
            return new Replacement(Property, Value, Path);
        }
    }

    public static class Ext
    {
        public static Wither<T> With1<T, TProperty>(this T instance, Expression<Func<T, TProperty>> propertyPicker, TProperty value) =>
            new Wither<T>(instance).With1(propertyPicker, value);
        
        public static Wither<T> With1<T, TProperty>(this T instance, Expression<Func<T, TProperty>> propertyPicker, Func<TProperty, TProperty> valueFactory) =>
            new Wither<T>(instance).With1(propertyPicker, valueFactory);
    }

    public class Wither<T>
    {
        private readonly T _instance;
        private readonly List<Replacement> _replacements = new();

        public Wither(T instance)
        {
            _instance = instance;
        }

        public Wither<T> With1<TProperty>(Expression<Func<T, TProperty>> propertyPicker, TProperty value)
        {
            _replacements.Add(ReplacementHelper.Create(propertyPicker, value));
            return this;
        }
        
        public Wither<T> With1<TProperty>(Expression<Func<T, TProperty>> propertyPicker, Func<TProperty, TProperty> valueFactory)
        {
            _replacements.Add(ReplacementHelper.Create(propertyPicker, valueFactory));
            return this;
        }

        public T Build()
        {
            return Program.With<T>(_replacements.ToArray())(_instance);
        }
        
        public static implicit operator T(Wither<T> wither) => wither.Build();
    }

    class ExpressionTreeWithBuilder
    {
        public static Func<T, T> With<T>(params Replacement[] replacements)
        {
            var instanceType = typeof(T);
            
            // Parameters
            var incomingInstance = Expression.Parameter(instanceType);
            
            // Variables
            var instanceVariable = Expression.Variable(instanceType);

            if (replacements.Length == 0)
            {
                return Expression.Lambda<Func<T, T>>(
                        body: Expression.Block(
                            new[] { instanceVariable },
                            Expression.Assign(instanceVariable, incomingInstance)
                        ),
                        parameters: incomingInstance
                    )
                    .Compile();
            }

            var body = new Stack<Expression>();
            var nestedReplacements = replacements
                .Where(r => r.IsNested)
                .ToLookup(r => r.Member);
                
            var dict = replacements
                .GroupBy(r => r.Property)
                .Select(g => g.Last())
                .ToDictionary(g => g.Property);

            
            var ctor = TypeUtils.GetSuitableConstructor<T>();

            // Instantiate the object
            body.Push(Expression.Assign(instanceVariable, Expression.New(ctor)));

            var properties = instanceType.GetProperties();
            foreach (var propertyInfo in properties)
            {
                var propertyExpression = Expression.Property(instanceVariable, propertyInfo);
                
                var nestedRepls = nestedReplacements[propertyInfo];

                if (nestedRepls.Any())
                {
                    var reps = nestedRepls
                        .Select(r => r.Lift())
                        .ToArray();
                    var method = typeof(Program)
                        .GetMethod(nameof(With))
                        .MakeGenericMethod(propertyInfo.PropertyType);
                    var methodCallExpression = Expression.Call(null, method, arguments: new []{ Expression.Constant(reps) });

                    var lambdaExpression = Expression.Lambda(methodCallExpression);

                    var expression = Expression.Lambda(Expression.Invoke(Expression.Invoke(lambdaExpression), propertyExpression), instanceVariable);
                    body.Push(Expression.Assign(propertyExpression, Expression.Invoke(expression, incomingInstance)));
                    continue;
                }

                if (false)
                {
                    var nestedProperty = Expression.Property(incomingInstance, propertyInfo);
                    var nestedInstance = Expression.Variable(nestedProperty.Type);
                    body.Push(Expression.Assign(nestedInstance, nestedProperty));
                    
                    var nestedProperties = nestedProperty.Type.GetProperties();
                    foreach (var nestedPropertyInfo in nestedProperties)
                    {
                        Expression newValue = dict.TryGetValue(nestedPropertyInfo, out var replacement)
                            ? Expression.Constant(replacement.Value)
                            : Expression.Property(nestedInstance, nestedPropertyInfo);
                        
                        // Assign property
                        body.Push(Expression.Assign(Expression.Property(nestedInstance, nestedPropertyInfo), newValue));
                    }
                    
                    body.Push(Expression.Assign(propertyExpression, nestedInstance));
                }
                else
                {
                    Expression newValue = dict.TryGetValue(propertyInfo, out var replacement)
                        ? Expression.Constant(replacement.Value)
                        : Expression.Property(incomingInstance, propertyInfo);

                    // Assign property
                    body.Push(Expression.Assign(propertyExpression, newValue));
                }
            }

            // Load the new instance on the stack
            body.Push(instanceVariable);

            var lambda = Expression.Lambda<Func<T, T>>(
                body: Expression.Block(new[] { instanceVariable }, body.Reverse()),
                parameters: incomingInstance
            );

            // Compile the expression tree
            return lambda.Compile();
        }
    }

    internal static class ReplacementHelper
    {
        public static Replacement Create<T, TProperty>(Expression<Func<T, TProperty>> propertyPicker, TProperty value)
        {
            return Create(propertyPicker, (object)value);
        }
        
        public static Replacement Create<T, TProperty>(Expression<Func<T, TProperty>> propertyPicker, Func<TProperty, TProperty> valueFactory)
        {
            return Create(propertyPicker, (object)valueFactory);
        }

        private static Replacement Create<T, TProperty>(Expression<Func<T, TProperty>> propertyPicker, object valueOrValueFactory)
        {
            var body = propertyPicker.Body;
            
            if (body is not MemberExpression { Member: PropertyInfo property } expression)
                throw new InvalidOperationException("Expression must be a property expression");

            // Nested property?
            if (expression.NodeType == ExpressionType.MemberAccess)
            {
                var stack = new Stack<MemberInfo>();
                var memberExpression = expression.Expression as MemberExpression;
                while (memberExpression?.NodeType == ExpressionType.MemberAccess)
                {
                    stack.Push(memberExpression.Member);
                    memberExpression = memberExpression.Expression as MemberExpression;
                }
                
                return new Replacement(property, valueOrValueFactory, stack);
            }
            
            return new Replacement(property, valueOrValueFactory, null);
        }
    }
    
    class Program
    {
        private static Func<T, T> With<T, TProperty>(Expression<Func<T, TProperty>> propertyPicker, TProperty value)
        {
            if (propertyPicker.Body is not MemberExpression { Member: PropertyInfo property })
                throw new InvalidOperationException("Expression must be a property expression");
            
            var body = new Stack<Expression>();
            var instanceType = typeof(T);

            // Parameters
            var incomingInstance = Expression.Parameter(instanceType);
            
            // Variables
            var instanceVariable = Expression.Variable(instanceType);
            
            var ctor = TypeUtils.GetSuitableConstructor<T>();

            // Instantiate the object
            body.Push(Expression.Assign(instanceVariable, Expression.New(ctor)));

            var properties = instanceType.GetProperties();
            foreach (var propertyInfo in properties)
            {
                Expression propertyValue = propertyInfo == property
                    ? Expression.Constant(value)
                    : Expression.Property(incomingInstance, propertyInfo);

                // Assign property
                body.Push(Expression.Assign(Expression.Property(instanceVariable, propertyInfo), propertyValue));
            }

            // Load the new instance on the stack
            body.Push(instanceVariable);

            var expression = Expression.Lambda<Func<T, T>>(
                body: Expression.Block(new[] { instanceVariable }, body.Reverse()),
                parameters: incomingInstance
            );

            // Compile the expression tree
            return expression.Compile();
        }

        private static Dictionary<ParameterInfo, PropertyInfo> CreateParameterInfoMap(ConstructorInfo constructor)
        {
            var map = new Dictionary<ParameterInfo, PropertyInfo>();

            var declaringType = constructor.DeclaringType ?? throw new Exception($"Method {constructor.Name} does not have a {nameof(ConstructorInfo.DeclaringType)}");
            
            var properties = declaringType
                .GetProperties()
                .Where(p => p.CanWrite)
                .ToDictionary(p => p.SetMethod);

            var parameterInfos = constructor.GetParameters();
            var instructions = constructor.GetInstructions();

            foreach (var instruction in instructions)
            {
                if (instruction.OpCode == OpCodes.Ldarg_0 || instruction.OpCode == OpCodes.Nop)
                {
                    continue;
                }

                if (instruction.OpCode.Name.StartsWith("ldarg"))
                {
                    int index = instruction.OpCode.Name switch
                    {
                        "ldarg.1" => 0,
                        "ldarg.2" => 1,
                        "ldarg.3" => 2,
                        "ldarg.s" => Array.IndexOf(parameterInfos, parameterInfos.Single(p => p == (ParameterInfo)instruction.Operand))
                    };
                    
                    var param = parameterInfos[index];
                    
                    if (instruction.Next?.OpCode == OpCodes.Call)
                    {
                        // Is this a property setter?
                        var callInstruction = instruction.Next;
                        if (callInstruction.Operand is MethodInfo methodInfo)
                        {
                            if (properties.TryGetValue(methodInfo, out var property))
                            {
                                map.Add(param, property);
                            }
                        }
                    }
                }
            }

            return map;
        }
        
        public static Func<T, T> With<T>(params Replacement[] replacements)
        {
            var instanceType = typeof(T);
            
            // Parameters
            var incomingInstance = Expression.Parameter(instanceType);
            
            // Variables
            var instanceVariable = Expression.Variable(instanceType);

            if (replacements.Length == 0)
            {
                return Expression.Lambda<Func<T, T>>(
                        body: Expression.Block(
                            new[] { instanceVariable },
                            Expression.Assign(instanceVariable, incomingInstance)
                        ),
                        parameters: incomingInstance
                    )
                    .Compile();
            }

            var body = new Stack<Expression>();
            var nestedReplacementsByMember = replacements
                .Where(r => r.IsNested)
                .ToLookup(r => r.Member);
                
            var replacementsByProperty = replacements
                .GroupBy(r => r.Property)
                // If there are more than one, we just need the most recent
                .Select(g => g.Last())
                .ToDictionary(g => g.Property);

            
            var ctor = TypeUtils.GetSuitableConstructor<T>();

            var parameterInfoMap = CreateParameterInfoMap(ctor);
            var array = ctor.GetParameters()
                .Select(p => parameterInfoMap[p])
                .Select<PropertyInfo, Expression>(p =>
                {
                    return GetReplacementExpression(p);

                    // var foundValue = replacementsByProperty.TryGetValue(p, out var replacement);
                    //
                    // if (!foundValue)
                    // {
                    //     return Expression.Property(incomingInstance, p);
                    // }
                    //
                    // if (replacement.IsFactory)
                    // {
                    //     var func = replacement.Value as Delegate;
                    //     return Expression.Invoke(Expression.Constant(func), Expression.Property(incomingInstance, p));
                    // }
                    //
                    // return Expression.Constant(replacement.Value);
                })
                .ToArray();

            // Instantiate the object
            body.Push(Expression.Assign(instanceVariable, Expression.New(ctor, array)));

            var properties = instanceType
                .GetProperties()
                .Where(p => p.CanWrite)
                .Except(parameterInfoMap.Values);
            foreach (var propertyInfo in properties)
            {
                var propertyExpression = Expression.Property(instanceVariable, propertyInfo);
                
                var nestedReplacements = nestedReplacementsByMember[propertyInfo].ToArray();

                if (nestedReplacements.Any())
                {
                    var liftedReplacements = nestedReplacements
                        .Select(r => r.Lift())
                        .ToArray();
                    var method = typeof(Program)
                        .GetMethod(nameof(With))
                        .MakeGenericMethod(propertyInfo.PropertyType);
                    var methodCallExpression = Expression.Call(null, method, arguments: new []{ Expression.Constant(liftedReplacements) });

                    var funcT2T = Expression.Invoke(Expression.Lambda(methodCallExpression));
                    var propertyWithWitherApplied = Expression.Invoke(funcT2T, propertyExpression);
                    var assignPropertyToInstance = Expression.Lambda(propertyWithWitherApplied, instanceVariable);
                    body.Push(Expression.Assign(propertyExpression, Expression.Invoke(assignPropertyToInstance, incomingInstance)));

                    continue;
                }

                // var foundValue = replacementsByProperty.TryGetValue(propertyInfo, out var replacement);
                //
                // Expression newValue;
                //
                // if (!foundValue)
                // {
                //     newValue = Expression.Property(incomingInstance, propertyInfo);
                // }
                // else
                // {
                //     if (replacement.IsFactory)
                //     {
                //         var func = replacement.Value as Delegate;
                //         newValue = Expression.Invoke(Expression.Constant(func), Expression.Property(incomingInstance, propertyInfo));
                //     }
                //     else
                //     {
                //         newValue = Expression.Constant(replacement.Value);
                //     }
                // }
                
                var newValue = GetReplacementExpression(propertyInfo);
                
                // Expression newValue = foundValue
                //     ? Expression.Constant(replacement.Value)
                //     : Expression.Property(incomingInstance, propertyInfo);

                // Assign property
                body.Push(Expression.Assign(propertyExpression, newValue));
            }

            // Load the new instance on the stack
            body.Push(instanceVariable);

            var lambda = Expression.Lambda<Func<T, T>>(
                body: Expression.Block(new[] { instanceVariable }, body.Reverse()),
                parameters: incomingInstance
            );

            // Compile the expression tree
            return lambda.Compile();

            Expression GetReplacementExpression(PropertyInfo propertyInfo)
            {
                var foundValue = replacementsByProperty.TryGetValue(propertyInfo, out var replacement);

                Expression newValue;

                if (!foundValue)
                {
                    return Expression.Property(incomingInstance, propertyInfo);
                }

                if (replacement.IsFactory)
                {
                    var func = replacement.Value as Delegate;
                    return Expression.Invoke(Expression.Constant(func), Expression.Property(incomingInstance, propertyInfo));
                }

                return Expression.Constant(replacement.Value);
            }
        }

        class Friend
        {
            public string Name { get; set; }
            public int Age { get; set; }
        }
        
        class Child
        {
            public string Name { get; set; }
            public Friend Friend { get; set; }
        }
        
        class Parent
        {
            public string Name { get; set; }
            public Child Child { get; set; }
        }

        class WithCtor
        {
            public string Name { get; private set; }
            public int Age { get; set; }
            public bool IsAdult => Age >= 18;

            // Many properties
            public string LastName { get; set; }
            public string Address { get; set; }
            public string City { get; set; }
            public string Country { get; set; }
            public string PostalCode { get; set; }
            public string PhoneNumber { get; set; }
            public string Email { get; set; }
            public string Fax { get; set; }
            public string Website { get; set; }
            public string Company { get; set; }
            public string Title { get; set; }
            public string Department { get; set; }

            public WithCtor(
                string name,
                string lastName,
                string address,
                string city,
                string country,
                string postalCode,
                string phoneNumber,
                string email,
                string fax,
                string website,
                string company,
                string title,
                string department
            )
            {
                Name = name;
                LastName = lastName;
                Address = address;
                City = city;
                Country = country;
                PostalCode = postalCode;
                PhoneNumber = phoneNumber;
                Email = email;
                Fax = fax;
                Website = website;
                Company = company;
                Title = title;
                Department = department;
            }
        }

        static void Main(string[] args)
        {
            {
                var parent = new Parent() { Name = "Hans", Child = new Child() { Name = "Søren", Friend = new Friend() { Name = "Lotte" } } };
                
                Parent wither = parent
                        // .With1(p => p.Child.Friend.Name, "Lasse")
                        .With1(p => p.Name, name => $"{name}2")
                        // .With1(p => p.Child.Friend.Age, 2)
                        // .With1(p => p.Child.Name, "Lotte")
                ;

                var withCtor = new WithCtor(
                    "Søren",
                    "Guldmund",
                    "Testvej 1",
                    "Testby",
                    "Testland",
                    "1234",
                    "12345678",
                    "e@e.dk",
                    "12345678",
                    "www.test.dk",
                    "TestCompany",
                    "TestTitle",
                    "TestDepartment"
                );

                WithCtor wither1 = withCtor
                        .With1(c => c.Name, name => $"{name}Lasse")
                        .With1(p => p.Age, 20)
                    ;

                var func1 = With<Parent>(
                    ReplacementHelper.Create((Expression<Func<Parent, string>>)(p => p.Child.Friend.Name), "Lasse"),
                    ReplacementHelper.Create((Expression<Func<Parent, int>>)(p => p.Child.Friend.Age), 2),
                    ReplacementHelper.Create((Expression<Func<Parent, string>>)(p => p.Name), "Hans"),
                    ReplacementHelper.Create((Expression<Func<Parent, string>>)(p => p.Child.Name), "Lotte")
                );

                var parent1 = func1(parent);

                // Using expression trees
                var student = new HogwartsStudents { Name = "Harry", House = House.Gryffindor };

                var with = With((HogwartsStudents s) => s.Name, "Draco");
                var students = with(student);

                var with1 = With<HogwartsStudents>();
                var students1 = with1(student);
                var func = With<HogwartsStudents>(
                    ReplacementHelper.Create((Expression<Func<HogwartsStudents, string>>)(s => s.Name), "Draco"),
                    ReplacementHelper.Create((Expression<Func<HogwartsStudents, House>>)(s => s.House), House.Slytherin),
                    ReplacementHelper.Create((Expression<Func<HogwartsStudents, string>>)(s => s.Name), "Harry")
                );
                var hogwartsStudents1 = func(student);

                var propertyPicker = ReplacementHelper.Create((Expression<Func<HogwartsStudents, string>>)(s => s.Name), "Snape");
                var member = propertyPicker.Property;
                var body = new Stack<Expression>();
                
                var ctor = TypeUtils.GetSuitableConstructor(student);
                var variable = Expression.Variable(typeof(HogwartsStudents));

                var parameterExpr = Expression.Parameter(typeof(HogwartsStudents));

                var newExpression = Expression.New(ctor);
                var assignVariable = Expression.Assign(variable, newExpression);
                body.Push(assignVariable);

                var properties = typeof(HogwartsStudents).GetProperties();
                foreach (var propertyInfo in properties)
                {
                    Expression value = propertyInfo == member
                        ? Expression.Constant(propertyPicker.Value)
                        : Expression.Property(Expression.Constant(student), propertyInfo);

                    var property = Expression.Property(variable, propertyInfo);
                    var assign = Expression.Assign(property, value);
                    
                    body.Push(assign);
                }

                body.Push(variable);
                
                var bodyExpression = Expression.Block(new []{variable}, body.Reverse());
                var expression = Expression.Lambda<Func<HogwartsStudents, HogwartsStudents>>(bodyExpression, parameterExpr);
                var compile = expression.Compile();
                var hogwartsStudents = compile.Invoke(student);
                Console.WriteLine("Hey");
            }
            {
                IStudent harry = new HogwartsStudents { Name = "Harry", House = House.Gryffindor }; 
                IStudent draco = harry.With(p => p.Name, "Draco");
                Console.WriteLine(draco); // Prints "Draco"
            }
            {
                var harry = new Student("Harry Potter", House.Gryffindor);
                var malfoy = harry
                    .With(p => p.Name, "Malfoy")
                    .With(p => p.House, house => house == House.Slytherin ? House.Gryffindor : house);

                Console.WriteLine(malfoy.Name); // Prints "Malfoy"
                Console.WriteLine(malfoy.House); // Prints "Gryffindor"
            }
            
            {
                var harry = new Student("Harry Potter", House.Gryffindor);
                var malfoy = harry
                    .With(p => p.Name, name => name.Length == 1 ? name : "Snape")
                    .With(p => p.House, House.Slytherin);
                Console.WriteLine(malfoy.Name);
            }
            
            var person = new Person("Søren", 10);
            Console.WriteLine(person);
            
            var lasse = person
                .With(p => p.Name, "Lasse");
            Console.WriteLine(lasse);
            
            var youngerSoren = person.With(p => p.Age, 5);
            Console.WriteLine(youngerSoren);
            
            var withLastName = person
                .With(p => p.Name, "Test")
                .With(p => p.LastName, "Guldmund")
                .With(p => p.Age, 5);
            Console.WriteLine(withLastName);
            
            var sorenAgain = person
                .With(p => p.Name, "Søren");
            Console.WriteLine(sorenAgain);
            
            var noCtor = new NoCtor {Name = "Søren"};
            Console.WriteLine(noCtor);
            
            var noCtor1 = noCtor.With(p => p.Name, "Test");
            Console.WriteLine(noCtor1);
        }
    }
}