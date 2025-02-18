using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Typesafe.With.Tests")]

namespace Typesafe.With
{
    public static class ObjectExtensions
    {
        public static T With<T, TProperty>(
            this T instance,
            Expression<Func<T, TProperty>> propertyPicker,
            Expression<Func<TProperty, TProperty>> propertyValueFactory
        )
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            if (propertyPicker == null) throw new ArgumentNullException(nameof(propertyPicker));

            var property = propertyPicker.GetProperty();
            var properties = new Dictionary<PropertyInfo, object>
            {
                {property, new DependentValue(propertyValueFactory)}
            };

            Validate(property, instance);
            
            var constructor = TypeUtils.GetSuitableConstructor(instance);
            var builder = new UnifiedWithBuilder<T>(constructor);
            
            return builder.Construct(instance, properties);
        }

        public static T With<T, TProperty>(
            this T instance,
            Expression<Func<T, TProperty>> propertyPicker,
            TProperty propertyValue)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            if (propertyPicker == null) throw new ArgumentNullException(nameof(propertyPicker));

            var property = propertyPicker.GetProperty();
            var properties = new Dictionary<PropertyInfo, object>
            {
                {property, propertyValue}
            };

            Validate(property, instance);
            
            var constructor = TypeUtils.GetSuitableConstructor(instance);
            var builder = new UnifiedWithBuilder<T>(constructor);
            
            return builder.Construct(instance, properties);
        }

        private static void Validate<T>(PropertyInfo propertyName, T instance)
        {
            // Can we set the property via constructor?
            var hasConstructorParameter = HasConstructorParameter(propertyName, instance);
            
            if (hasConstructorParameter) return;
            
            // Can we set the property via property setter?
            var hasPropertySetter = HasPropertySetter(propertyName);
            
            if (hasPropertySetter) return;

            // If we cannot do either, then there is no point in continuing.
            throw new InvalidOperationException(
                $"Error calling {nameof(With)} on type {typeof(T)}: Property '{propertyName.Name}' cannot be set via constructor or property setter. You can fix this by making the property settable or adding it as a constructor parameter."
            );
        }

        private static bool HasPropertySetter(PropertyInfo propertyName)
        {
            return propertyName.CanWrite;
        }

        private static bool HasConstructorParameter<T>(PropertyInfo propertyName, T instance)
        {
            var constructorParameters = TypeUtils.GetSuitableConstructor(instance).GetParameters();
            
            // Can we find a matching constructor parameter?
            var hasConstructorParameter = constructorParameters
                .Any(info => string.Equals(info.Name, propertyName.Name.ToParameterCase(), StringComparison.Ordinal));
            
            if (hasConstructorParameter) return true;

            // Can we find a matching constructor parameter if we lowercase both parameter and property name?
            var hasConstructorParameterByLowercase = constructorParameters
                .Any(info => string.Equals(info.Name, propertyName.Name.ToParameterCase(), StringComparison.InvariantCultureIgnoreCase));

            if (hasConstructorParameterByLowercase) return true;

            return false;
        }
    }
}