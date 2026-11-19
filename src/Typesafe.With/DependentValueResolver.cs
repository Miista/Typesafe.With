using System.Reflection;

namespace Typesafe.With
{
    internal class DependentValueResolver<TInstance>(TInstance instance)
        where TInstance : notnull
    {
        public object Resolve(DependentValue dependentValue, PropertyInfo existingProperty)
        {
            return dependentValue.Resolve(existingProperty, instance);
        }
    }
}