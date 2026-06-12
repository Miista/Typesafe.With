using AutoFixture;

namespace Typesafe.With.Tests
{
    public sealed class TypeWithDeeplyNestedPropertyCustomization : ICustomization
    {
        private readonly int _count;

        public TypeWithDeeplyNestedPropertyCustomization(int count) => _count = count;

        public void Customize(IFixture fixture)
        {
            fixture.Register(() =>
                {
                    var root = new Tests.NestedProperties.TypeWithDeeplyNestedProperty
                    {
                        Text = fixture.Create<string>(),
                        Nested = null
                    };

                    if (_count == 0) return root;

                    for (var i = 0; i < _count; i++)
                    {
                        root = new Tests.NestedProperties.TypeWithDeeplyNestedProperty { Nested = root };
                    }

                    return root;
                }
            );
        }
    }
}