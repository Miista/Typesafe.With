using System;
using FluentAssertions;
using Xunit;

namespace Typesafe.With.Tests;

public abstract partial class Tests
{
    public abstract class RecordTests
    {
        public class RecordClasses
        {
            internal record Record(string Name, int Age);

            [Fact]
            public void Same()
            {
                // Arrange
                var instance = new Record("Harry", 0);

                // Act
                var malfoyWith = instance with { Name = "Malfoy" };
                var our = instance.With(x => x.Name, "Malfoy");

                // Assert
                malfoyWith.Should().BeEquivalentTo(our);
            }
        }
        
        public class RecordStructs
        {
            internal record struct Record(string Name, int Age);

            private static void T(Record r)
            {
                var x = r with { Name = "Test" };
            }
            
            [Fact]
            public void Same()
            {
                // Arrange
                Record instance = new Record("Harry", 0);
                T(instance);
                Console.WriteLine(instance);
                
                // Act
                var malfoyWith = instance with { Name = "Malfoy" };
                var our = instance.With(x => x.Name, "Malfoy");

                // Assert
                malfoyWith.Should().BeEquivalentTo(our);
            }
        }
        
        public class ReadonlyRecordStructs
        {
            internal readonly record struct Record(string Name, int Age);

            [Fact]
            public void Same()
            {
                // Arrange
                var instance = new Record("Harry", 0);

                // Act
                var malfoyWith = instance with { Name = "Malfoy" };
                var our = instance.With(x => x.Name, "Malfoy");

                // Assert
                malfoyWith.Should().BeEquivalentTo(our);
            }
        }
    }
}