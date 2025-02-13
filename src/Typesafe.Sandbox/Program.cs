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
            
            var ctor = TypeUtils.GetSuitableConstructor1<T>();

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

        public static Func<T, T> With<T>(params Replacement[] replacements) => ExpressionTreeWithBuilder.With<T>(replacements);

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

        class Base
        {
            public string Name { get; }

            public Base(string name)
            {
                Name = name;
            }
        }
        
        class Parent : Base
        {
            public Child Child { get; set; }

            public Parent(string name) : base(name)
            {
                
            }
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

        static Expression<Func<T, ConstructorInfo>> GetConstructor<T>()
        {
            var incomingInstance = Expression.Parameter(typeof(T));
            var instanceVariable = Expression.Variable(typeof(ConstructorInfo));

            var getSuitableCtorMethod = typeof(TypeUtils)
                .GetMethod(nameof(TypeUtils.GetSuitableConstructor))
                .MakeGenericMethod(typeof(T));
            var callExpression = Expression.Call(null, getSuitableCtorMethod, incomingInstance);

            var body = new Stack<Expression>();
            body.Push(Expression.Assign(instanceVariable, callExpression));

            var lambda = Expression.Lambda<Func<T, ConstructorInfo>>(
                body: Expression.Block(
                    variables: new[] { instanceVariable },
                    expressions: body.Reverse()
                ),
                parameters: incomingInstance
            );

            return lambda;
        }
        
        static void Main(string[] args)
        {
            {
                IPerson person1 = new HogwartsStudents();
                
                // Get constructor
                var constructor = GetConstructor<IPerson>();

                var incomingInstance = Expression.Parameter(typeof(IPerson));
                var lambda = Expression.Lambda<Func<IPerson, ConstructorInfo>>(
                    body: Expression.Block(
                        expressions: Expression.Invoke(constructor, incomingInstance)
                    ),
                    parameters: incomingInstance
                );

                var compile = lambda.Compile();
                var constructorInfo = compile.Invoke(person1);

                var methodInfo = typeof(Typesafe.With.ExpressionTreeWithBuilder).GetMethod(nameof(Typesafe.With.ExpressionTreeWithBuilder.CreateParameterInfoMap));
                var methodCallExpression = Expression.Call(null, methodInfo, Expression.Constant(constructorInfo), Expression.Constant(null, typeof(PropertyInfo[])));
                
                var methodInfoParam = Expression.Parameter(typeof(MethodInfo));
                var constructorInfoParam = Expression.Parameter(typeof(ConstructorInfo));
                
                var lambda2 = Expression.Lambda<Func<MethodInfo, ConstructorInfo, Dictionary<ParameterInfo, PropertyInfo>>>(
                    body: Expression.Block(
                        expressions: methodCallExpression
                    ),
                    parameters: new[] { methodInfoParam, constructorInfoParam }
                );

                var compile2 = lambda2.Compile();
                var propertyInfos = compile2.Invoke(methodInfo, constructorInfo);

                var parameterInfoMap = Typesafe.With.ExpressionTreeWithBuilder.CreateParameterInfoMap(constructorInfo);
            }
            
            {
                var parent = new Parent("Hans") { Child = new Child() { Name = "Søren", Friend = new Friend() { Name = "Lotte" } } };
                
                Parent wither = parent
                        // .With1(p => p.Child.Friend.Name, "Lasse")
                        .With(p => p.Name, name => $"{name}2")
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
                        .With(c => c.Name, name => $"{name}Lasse")
                        .With(p => p.Age, 20)
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
                IStudent draco = ObjectExtensions.With(harry, p => p.Name, "Draco");
                Console.WriteLine(draco); // Prints "Draco"
            }
            {
                var harry = new Student("Harry Potter", House.Gryffindor);
                Student malfoy = ObjectExtensions
                    .With<Student, House>(
                        harry
                            .With(p => p.Name, "Malfoy"), p => p.House, house => house == House.Slytherin ? House.Gryffindor : house);

                Console.WriteLine(malfoy.Name); // Prints "Malfoy"
                Console.WriteLine(malfoy.House); // Prints "Gryffindor"
            }
            
            {
                var harry = new Student("Harry Potter", House.Gryffindor);
                var malfoy = ObjectExtensions
                    .With<Student, House>(
                        harry
                            .With(p => p.Name, name => name.Length == 1 ? name : "Snape"), p => p.House, House.Slytherin);
                Console.WriteLine(malfoy.Name);
            }
            
            var person = new Person("Søren", 10);
            Console.WriteLine(person);
            
            var lasse = ObjectExtensions.With(person, p => p.Name, "Lasse");
            Console.WriteLine(lasse);
            
            var youngerSoren = ObjectExtensions.With(person, p => p.Age, 5);
            Console.WriteLine(youngerSoren);
            
            var withLastName = ObjectExtensions
                .With<Person, int>(
                    person
                        .With(p => p.Name, "Test")
                        .With(p => p.LastName, "Guldmund"), p => p.Age, 5);
            Console.WriteLine(withLastName);
            
            var sorenAgain = ObjectExtensions.With(person, p => p.Name, "Søren");
            Console.WriteLine(sorenAgain);
            
            var noCtor = new NoCtor {Name = "Søren"};
            Console.WriteLine(noCtor);
            
            var noCtor1 = ObjectExtensions.With(noCtor, p => p.Name, "Test");
            Console.WriteLine(noCtor1);
        }
    }
}