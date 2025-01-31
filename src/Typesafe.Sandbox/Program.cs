using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
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
    
    class Program
    {
        // static (Expression<Func<T, TProperty>> Expression, TProperty Value) PropertyPicker<T, TProperty>(Expression<Func<T, TProperty>> propertyPicker, TProperty value) => (propertyPicker, value);
        
        static Replacement PropertyPicker<T, TProperty>(Expression<Func<T, TProperty>> propertyPicker, TProperty value)
        {
            var body = propertyPicker.Body;
            
            if (body is not MemberExpression { Member: PropertyInfo property })
                throw new InvalidOperationException("Expression must be a property expression");

            if (body.NodeType == ExpressionType.MemberAccess)
            {
                var stack = new Stack<MemberInfo>();
                MemberExpression x = (body as MemberExpression).Expression as MemberExpression;
                while (x?.NodeType == ExpressionType.MemberAccess)
                {
                    stack.Push(x.Member);
                    x = x.Expression as MemberExpression;
                }
                // Nested property?
                // if (body is MemberExpression { Expression: MemberExpression { Member: PropertyInfo nestedProperty } })
                // {
                    return new Replacement(property, value, stack);
                // }
            }
            return new Replacement(property, value, null);
        }

        static Func<T, T> With<T, TProperty>(Expression<Func<T, TProperty>> propertyPicker, TProperty value)
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
            public Child Child { get; set; }
        }
        
        static void Main(string[] args)
        {
            {
                var parent = new Parent() { Child = new Child() { Name = "Søren", Friend = new Friend() { Name = "Lotte" } } };

                var func1 = With<Parent>(
                    PropertyPicker<Parent, string>(p => p.Child.Friend.Name, "Lasse"),
                    PropertyPicker<Parent, int>(p => p.Child.Friend.Age, 2)
                );

                var parent1 = func1(parent);

                // Using expression trees
                var student = new HogwartsStudents { Name = "Harry", House = House.Gryffindor };

                var with = With((HogwartsStudents s) => s.Name, "Draco");
                var students = with(student);

                var with1 = With<HogwartsStudents>();
                var students1 = with1(student);
                var func = With<HogwartsStudents>(
                    PropertyPicker<HogwartsStudents, string>(s => s.Name, "Draco"),
                    PropertyPicker<HogwartsStudents, House>(s => s.House, House.Slytherin),
                    PropertyPicker<HogwartsStudents, string>(s => s.Name, "Harry")
                );
                var hogwartsStudents1 = func(student);

                var propertyPicker = PropertyPicker<HogwartsStudents, string>(s => s.Name, "Snape");
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