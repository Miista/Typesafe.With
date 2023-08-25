using System;
using System.Linq;
using Typesafe.With.Sequence;
using Typesafe.With.Lazy;

namespace Typesafe.Sandbox
{
    class Student
    {
        public string Name { get; }
        public House House { get; }
    
        public Student(string name, House house) => (Name, House) = (name, house);
    }

    enum House { Gryffindor, Slytherin }
    
    class Program
    {
        static void Main(string[] args)
        {
            {
                var harry = new Student("Harry Potter", House.Gryffindor);

                var hermione = harry
                    .With(_ => _.Name, "Hermione")
                    .With(_ => _.House, House.Slytherin);

                Console.WriteLine(hermione); // Prints: Typesafe.With.Lazy.LazyInstancedWithSequence`1[Typesafe.Sandbox.Student]
                Console.WriteLine(hermione.Apply().Name); // Prints: Hermione - Slytherin
            }
            {
                var sequence = WithSequence
                    .New<Student>().With<House>(_ => _.House, House.Gryffindor)
                    .ToSequence();

                var students = new[]
                {
                    new Student("Harry Potter", House.Slytherin),
                    new Student("Ron Weasley", House.Slytherin),
                    new Student("Hermione Granger", House.Slytherin)
                };

                var updatedStudents = students.Select(sequence.ApplyTo).ToArray();

                Console.WriteLine(updatedStudents[0].House); // Prints: Gryffindor
                Console.WriteLine(updatedStudents[1].House); // Prints: Gryffindor
                Console.WriteLine(updatedStudents[2].House); // Prints: Gryffindor
            }
        }
    }
}