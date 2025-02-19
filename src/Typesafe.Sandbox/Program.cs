using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
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

    class Lol
    {
        private string _name;
        
        public string Name
        {
            set => _name = value;
        }
    }

    class TT(int age)
    {
        
    }
    class TestBase(string name, int age) : TT(age)
    {
        
    }

    class Test(string name) : TestBase(name, 10)
    {
        public string Name => name;
    }
    
    class Test1 : TestBase
    {
        public string Name { get; }
        
        public Test1(string name) : base(name, 10)
        {
            Name = name;
        }
    }
    
    public record RecordClass(string Name, int Age);
    
    public record struct RecordStruct(string Name, int Age);
    
    public readonly record struct RecordReadonlyStruct(string Name, int Age);
    
    class Program
    {
        static bool IsRecord<T>()
        {
            var type = typeof(T);
            
            /*
             * Records have the following criteria:
             * 1. Deconstruct method
             * 2. Implement IEquatable<T> where T is the record type
             * 3. Equality operators: == and !=
             * 4. In case of record classes:
             * 4a. A compiler generated clone method
             * 4b. A compiler generated EqualityContract property
             */
            
            // 1. Deconstruct method
            var deconstructMethod = type.GetMethod("Deconstruct", BindingFlags.Instance | BindingFlags.Public);
            
            if (deconstructMethod == null) return false;
            
            // 2. Implement IEquatable<T> where T is the record type
            var genericEquatableInterface = typeof(IEquatable<>).MakeGenericType(type);
            var equatableInterface = type.GetInterface(genericEquatableInterface.Name);
            
            if (equatableInterface == null) return false;
            
            // 3. Equality operators: == and !=
            var equalityOperator = type.GetMethod("op_Equality", BindingFlags.Static | BindingFlags.Public);
            var inequalityOperator = type.GetMethod("op_Inequality", BindingFlags.Static | BindingFlags.Public);
            
            if (equalityOperator == null || inequalityOperator == null) return false;

            // 4. In case of record classes:
            if (type.IsClass)
            {
                // 4a. A compiler generated clone method
                // Structs do not need the clone method. Simply assigning the struct to a new variable, creates a copy.
                var cloneMethod = type.GetMethod("<Clone>$", BindingFlags.Instance | BindingFlags.Public);

                if (cloneMethod == null) return false;
                
                // 4b. A compiler generated EqualityContract property
                var equalityContractProperty = type.GetProperty("EqualityContract", BindingFlags.Instance | BindingFlags.NonPublic);
                
                if (equalityContractProperty == null) return false;
            }

            return true;
        }

        static T With1<T, TProperty>(T instance, Expression<Func<T, TProperty>> picker, TProperty value)
        {
            if (!IsRecord<T>())
            {
                return instance.With(picker, value);
            }
            else
            {
                T newInstance;
                var propertyInfo = picker.GetProperty() ?? throw new Exception();
                var propertyInfoSetMethod = propertyInfo.SetMethod ?? throw new Exception();
                
                if (typeof(T).IsValueType)
                {
                    newInstance = instance;
                    propertyInfoSetMethod.Invoke(newInstance, new object[] { value });
                }
                else
                {
                    var methodInfo = typeof(T).GetMethod("<Clone>$") ?? throw new Exception();
                    
                    newInstance = (T) methodInfo.Invoke(instance, new object[0]);
                    propertyInfoSetMethod.Invoke(newInstance, new object[] { value });
                    
                }
                
                return newInstance;
            }
        }
        
        static void Main(string[] args)
        {
            {
                var recordClass = new RecordClass("Søren", 10);
                var record1 = recordClass with { Name = "Lasse" };
                
                var recordStruct = new RecordStruct("Søren", 10);
                Console.WriteLine(recordStruct);
                var x = recordStruct;
                recordStruct.Name = "Ost";
                var newRecordStruct = recordStruct with { Name = "Lasse" };
                Console.WriteLine(newRecordStruct);
                
                var recordReadonlyStruct = new RecordReadonlyStruct("Søren", 10);
                var readonlyStruct = recordReadonlyStruct with { Name = "Lasse" };
                Console.WriteLine(IsRecord<RecordClass>());
                Console.WriteLine(IsRecord<RecordStruct>());
                Console.WriteLine(IsRecord<RecordReadonlyStruct>());
                With1<RecordClass, string>(record1, x => x.Name, "Søren");
                With1<RecordStruct, string>(newRecordStruct, x => x.Name, "Søren");
                With1<RecordReadonlyStruct, string>(readonlyStruct, x => x.Name, "Søren");
                
                var with = record1.With(x => x.Age, 15);
                Console.WriteLine(record1);
            }
            {
                var constructorInfo = typeof(Test).GetConstructors().First();
                var parameterInfoMap = ConstructorHelper.CreateParameterInfoMap(constructorInfo);
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