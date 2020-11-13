﻿using System.Collections;
using System.Collections.Generic;
using FluentAssertions;
using Typed.With;
using Xunit;

namespace TypeWither.Tests
{
    internal class SourceWithConstructor {
        public string Id { get; }
        public string Name { get; }
        public int? Age { get; }

        public SourceWithConstructor(string id, string name, int? age) => (Id, Name, Age) = (id, name, age);
    }

    internal class SourceWithSetters {
        public string Id { get; set; }
        public string Name { get; set; }
        public int? Age { get; set; }
    }

    internal class SourceWithMixedConstructorAndSetters {
        public string Id { get; }
        public string Name { get; }
        public int? Age { get; set; }

        public SourceWithMixedConstructorAndSetters(string id, string name) => (Id, Name) = (id, name);
    }

    public class TypeWitherTests
    {
        public class UsingPropertySetters
        {
            [Theory]
            [ClassData(typeof(TestData))]
            public void Can_with_using_properties(
                string sourceId, string sourceName, int? sourceAge,
                string withId, string withName, int? withAge,
                string expectedId, string expectedName, int? expectedAge)
            {
                // Arrange
                var source = new SourceWithSetters
                {
                    Id = sourceId,
                    Name = sourceName,
                    Age = sourceAge
                };
            
                // Act
                SourceWithSetters result = source
                    .With(_ => _.Id, withId)
                    .With(_ => _.Name, withName)
                    .With(_ => _.Age, withAge);

                // Assert
                result.Age.Should().Be(expectedAge);
                result.Id.Should().Be(expectedId);
                result.Name.Should().Be(expectedName);
            }

            [Theory]
            [ClassData(typeof(TestData))]
            public void Can_with_using_expressions_using_properties(
                string sourceId, string sourceName, int? sourceAge,
                string withId, string withName, int? withAge,
                string expectedId, string expectedName, int? expectedAge)
            {
                // Arrange
                var source = new SourceWithSetters
                {
                    Id = sourceId,
                    Name = sourceName,
                    Age = sourceAge
                };
            
                // Act
                SourceWithSetters result = source
                    .With(_ => _.Id, withId)
                    .With(_ => _.Age, withAge)
                    .With(_ => _.Name, withName);

                // Assert
                result.Age.Should().Be(expectedAge);
                result.Id.Should().Be(expectedId);
                result.Name.Should().Be(expectedName);
            }
            
            [Theory]
            [ClassData(typeof(TestData))]
            public void Can_with_using_properties_by_extension_with_method(
                string sourceId, string sourceName, int? sourceAge,
                string withId, string withName, int? withAge,
                string expectedId, string expectedName, int? expectedAge)
            {
                // Arrange
                var source = new SourceWithSetters
                {
                    Id = sourceId,
                    Name = sourceName,
                    Age = sourceAge
                };
            
                // Act
                SourceWithSetters result = source
                    .With(_ => _.Id, withId)
                    .With(_ => _.Name, withName)
                    .With(_ => _.Age, withAge);

                // Assert
                result.Age.Should().Be(expectedAge);
                result.Id.Should().Be(expectedId);
                result.Name.Should().Be(expectedName);
            }
        }

        public class UsingConstructor
        {
            [Theory]
            [ClassData(typeof(TestData))]
            public void Can_with_using_constructor(
                string sourceId, string sourceName, int? sourceAge,
                string withId, string withName, int? withAge,
                string expectedId, string expectedName, int? expectedAge)
            {
                // Arrange
                var source = new SourceWithConstructor(sourceId, sourceName, sourceAge);

                // Act
                SourceWithConstructor result = source
                    .With(_ => _.Id, withId)
                    .With(_ => _.Name, withName)
                    .With(_ => _.Age, withAge);

                // Assert
                result.Age.Should().Be(expectedAge);
                result.Id.Should().Be(expectedId);
                result.Name.Should().Be(expectedName);
            }
        
            [Theory]
            [ClassData(typeof(TestData))]
            public void Can_with_using_expressions_using_constructor(
                string sourceId, string sourceName, int? sourceAge,
                string withId, string withName, int? withAge,
                string expectedId, string expectedName, int? expectedAge)
            {
                // Arrange
                var source = new SourceWithConstructor(sourceId, sourceName, sourceAge);

                // Act
                SourceWithConstructor result = source
                    .With(_ => _.Age, withAge)
                    .With(_ => _.Id, withId)
                    .With(_ => _.Name, withName);

                // Assert
                result.Age.Should().Be(expectedAge);
                result.Id.Should().Be(expectedId);
                result.Name.Should().Be(expectedName);
            }
            
            [Theory]
            [ClassData(typeof(TestData))]
            public void Can_with_using_constructor_by_extension_with_method(
                string sourceId, string sourceName, int? sourceAge,
                string withId, string withName, int? withAge,
                string expectedId, string expectedName, int? expectedAge)
            {
                // Arrange
                var source = new SourceWithConstructor(sourceId, sourceName, sourceAge);

                // Act
                SourceWithConstructor result = source
                    .With(_ => _.Id, withId)
                    .With(_ => _.Name, withName)
                    .With(_ => _.Age, withAge);

                // Assert
                result.Age.Should().Be(expectedAge);
                result.Id.Should().Be(expectedId);
                result.Name.Should().Be(expectedName);
            }
        }

        public class UsingMixedConstructorAndPropertySetters
        {
            [Theory]
            [ClassData(typeof(TestData))]
            public void Can_with_using_mixed_constructor_and_property_setters(
                string sourceId, string sourceName, int? sourceAge,
                string withId, string withName, int? withAge,
                string expectedId, string expectedName, int? expectedAge)
            {
                // Arrange
                var source = new SourceWithMixedConstructorAndSetters(sourceId, sourceName) {Age = sourceAge};

                // Act
                SourceWithMixedConstructorAndSetters result = source
                    .With(_ => _.Id, withId)
                    .With(_ => _.Name, withName)
                    .With(_ => _.Age, withAge);

                // Assert
                result.Age.Should().Be(expectedAge);
                result.Id.Should().Be(expectedId);
                result.Name.Should().Be(expectedName);
            }
        
            [Theory]
            [ClassData(typeof(TestData))]
            public void Can_with_using_expressions_using_mixed_constructor_and_property_setters(
                string sourceId, string sourceName, int? sourceAge,
                string withId, string withName, int? withAge,
                string expectedId, string expectedName, int? expectedAge)
            {
                // Arrange
                var source = new SourceWithMixedConstructorAndSetters(sourceId, sourceName) {Age = sourceAge};

                // Act
                SourceWithMixedConstructorAndSetters result = source
                    .With(_ => _.Age, withAge)
                    .With(_ => _.Id, withId)
                    .With(_ => _.Name, withName);

                // Assert
                result.Age.Should().Be(expectedAge);
                result.Id.Should().Be(expectedId);
                result.Name.Should().Be(expectedName);
            }
            
            [Theory]
            [ClassData(typeof(TestData))]
            public void Can_with_using_mixed_constructor_and_property_setters_by_extension_with_method(
                string sourceId, string sourceName, int? sourceAge,
                string withId, string withName, int? withAge,
                string expectedId, string expectedName, int? expectedAge)
            {
                // Arrange
                var source = new SourceWithMixedConstructorAndSetters(sourceId, sourceName){Age = sourceAge};

                // Act
                SourceWithMixedConstructorAndSetters result = source
                    .With(_ => _.Id, withId)
                    .With(_ => _.Name, withName)
                    .With(_ => _.Age, withAge);

                // Assert
                result.Age.Should().Be(expectedAge);
                result.Id.Should().Be(expectedId);
                result.Name.Should().Be(expectedName);
            }
        }
        
        public class TestData : IEnumerable<object[]>
        {
            public IEnumerator<object[]> GetEnumerator()
            {
                yield return new object[]{null, null, null, null, null, null, null, null, null};
                yield return new object[]{null, null, 1,    null, null, null, null, null, null};
                yield return new object[]{null, "1",  null, null, null, null, null, null, null};
                yield return new object[]{null, "1",  1,    null, null, null, null, null, null};
                yield return new object[]{"1",  null, null, null, null, null, null, null, null};
                yield return new object[]{"1",  null, 1,    null, null, null, null, null, null};
                yield return new object[]{"1",  "1",  null, null, null, null, null, null, null};
                yield return new object[]{"1",  "1",  1,    null, null, null, null, null, null};
                yield return new object[]{null, null, null, null, null, 2,    null, null, 2};
                yield return new object[]{null, null, null, null, "2",  null, null, "2",  null};
                yield return new object[]{null, null, null, null, "2",  2,    null, "2",  2};
                yield return new object[]{null, null, null, "2",  null, null, "2",  null, null};
                yield return new object[]{null, null, null, "2",  null, 2,    "2",  null, 2};
                yield return new object[]{null, null, null, "2",  "2",  null, "2",  "2",  null};
                yield return new object[]{null, null, null, "2",  "2",  2,    "2",  "2",  2};
                yield return new object[]{null, null, 1,    null, null, 2,    null, null, 2};
                yield return new object[]{null, "1",  null, null, "2",  null, null, "2",  null};
                yield return new object[]{null, "1",  1,    null, "2",  2,    null, "2",  2};
                yield return new object[]{"1",  null, null, "2",  null, null, "2",  null, null};
                yield return new object[]{"1",  null, 1,    "2",  null, 2,    "2",  null, 2};
                yield return new object[]{"1",  "1",  1,    "2",  "2",  2,    "2",  "2",  2};
                yield return new object[]{"1",  "1",  1,    null, null, null, null, null, null};
                yield return new object[]{"1",  "1",  1,    null, null, 2,    null, null, 2};
                yield return new object[]{"1",  "1",  1,    null, "2",  null, null, "2",  null};
                yield return new object[]{"1",  "1",  1,    null, "2",  2,    null, "2",  2};
                yield return new object[]{"1",  "1",  1,    "2",  null, null, "2",  null, null};
                yield return new object[]{"1",  "1",  1,    "2",  null, 2,    "2",  null, 2};
                yield return new object[]{"1",  "1",  1,    "2",  "2",  null, "2",  "2",  null};
                yield return new object[]{"1",  "1",  1,    "2",  "2",  2,    "2",  "2",  2};
                yield return new object[]{null, null, null, "2",  "2",  2,    "2",  "2",  2};
                yield return new object[]{null, null, 1,    "2",  "2",  2,    "2",  "2",  2};
                yield return new object[]{null, "1", null,  "2",  "2",  2,    "2",  "2",  2};
                yield return new object[]{null, "1", 1,     "2",  "2",  2,    "2",  "2",  2};
                yield return new object[]{"1",  null, null, "2",  "2",  2,    "2",  "2",  2};
                yield return new object[]{"1",  null, 1,    "2",  "2",  2,    "2",  "2",  2};
                yield return new object[]{"1",  "1",  null, "2",  "2",  2,    "2",  "2",  2};
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}