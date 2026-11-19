using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Typesafe.With
{
    public static class ObjectExtensions
    {
        extension<T>(T instance)
            where T : notnull
        {
            
            public T With<TProperty>(
                Expression<Func<T, TProperty>> propertyPicker,
                Expression<Func<TProperty, TProperty>> propertyValueFactory
            )
            {
                if (instance is null) throw new ArgumentNullException(nameof(instance));
                if (propertyPicker is null) throw new ArgumentNullException(nameof(propertyPicker));

                var propertyName = propertyPicker.PropertyName;
                var properties = new Dictionary<string, object?>
                {
                    {propertyName, new DependentValue(propertyValueFactory)}
                };

                instance.Validate(propertyName);
                
                var constructor = instance.GetSuitableConstructor();
                var builder = new UnifiedWithBuilder<T>(constructor);
                
                return builder.Construct(instance, properties);
            }

            public T With<TProperty>(
                Expression<Func<T, TProperty>> propertyPicker,
                TProperty propertyValue)
            {
                if (instance is null) throw new ArgumentNullException(nameof(instance));
                if (propertyPicker is null) throw new ArgumentNullException(nameof(propertyPicker));

                var propertyName = propertyPicker.PropertyName;
                var properties = new Dictionary<string, object?>
                {
                    {propertyName, propertyValue}
                };

                instance.Validate(propertyName);
                
                var constructor = instance.GetSuitableConstructor();
                var builder = new UnifiedWithBuilder<T>(constructor);
                
                return builder.Construct(instance, properties);
            }

            private void Validate(string propertyName)
            {
                // Can we set the property via constructor?
                var hasConstructorParameter = instance.HasConstructorParameter(propertyName);
                
                if (hasConstructorParameter) return;
                
                // Can we set the property via property setter?
                var hasPropertySetter = instance.HasPropertySetter(propertyName);
                
                if (hasPropertySetter) return;

                // If we cannot do either, then there is no point in continuing.
                throw new InvalidOperationException(
                    $"Error calling {nameof(With)} on type {typeof(T)}: Property '{propertyName.ToPropertyCase()}' cannot be set via constructor or property setter. You can fix this by making the property settable or adding it as a constructor parameter."
                );
            }

            private bool HasPropertySetter(string propertyName)
            {
                return instance.PropertyDictionary.TryGetValue(propertyName, out var propertyInfo) && propertyInfo.CanWrite;
            }

            //return TypeUtils.GetPropertyDictionary<T>().TryGetValue(propertyName, out var propertyInfo) && propertyInfo.CanWrite;
            private bool HasConstructorParameter(string propertyName)
            {
                var constructorParameters = instance.GetSuitableConstructor().GetParameters();
                
                // Can we find a matching constructor parameter?
                var hasConstructorParameter = constructorParameters
                    .Any(info => string.Equals(info.Name, propertyName, StringComparison.Ordinal));
                
                if (hasConstructorParameter) return true;

                // Can we find a matching constructor parameter if we lowercase both parameter and property name?
                var hasConstructorParameterByLowercase = constructorParameters
                    .Any(info => string.Equals(info.Name, propertyName, StringComparison.InvariantCultureIgnoreCase));

                if (hasConstructorParameterByLowercase) return true;

                return false;
            }
        }
    }
}