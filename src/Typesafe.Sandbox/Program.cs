using System;
using Domain;
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

    public class HogwartsStudents : IStudent
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

    public class NoCtor
    {
        public string Name { get; set; }

        public override string ToString() => $"Name={Name};";
    }

    class UnrelatedType
    {
    }
    
    public class Student
    {
        public string Name { get; }
        public House House { get; }
    
        public Student(string name, House house) => (Name, House) = (name, house);
    }

    public enum House
    {
        Gryffindor,
        Slytherin
    }
    
    class Program
    {
        private class Test
        {
            public string Name { get; set; }
        }
        
        static void Main(string[] args)
        {
            {
                var person1 = new Person("Name", 10);
                var student = new Student("Harry", House.Gryffindor);
                var withHouse = student.WithHouse(House.Slytherin).WithName("Malfoy");
                var class1 = new Class1();
                var withName = class1.WithName("x");
                // Console.WriteLine(class1.WithName("Hey"));
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