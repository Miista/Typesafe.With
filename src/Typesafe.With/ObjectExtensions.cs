using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Typesafe.With
{
    /// <summary>
    /// Extension methods for creating modified copies of objects in a type-safe manner.
    /// </summary>
    /// <remarks>
    /// The original instance is never modified. A new instance is created via the public constructor with the most parameters,
    /// and remaining properties are set via property setters or copied from the original instance.
    /// </remarks>
    public static class ObjectExtensions
    {
        /// <typeparam name="T">The type of the instance.</typeparam>
        /// <param name="instance">The instance to copy.</param>
        extension<T>(T instance)
            where T : notnull
        {
            /// <summary>
            /// Returns a copy of the instance with the selected property reset to <c>default(TProperty)</c>.
            /// </summary>
            /// <remarks>
            /// The property is set to <c>null</c> for reference types and nullable value types (e.g. <c>int?</c>),
            /// and to the type's default for non-nullable value types (e.g. <c>0</c> for <c>int</c>).
            /// Nullable reference type annotations are not enforced: a non-nullable reference property is set to <c>null</c>.
            /// </remarks>
            /// <param name="propertyPicker">An expression selecting a property directly on the instance, e.g. <c>p =&gt; p.Name</c>.</param>
            /// <typeparam name="TProperty">The type of the property.</typeparam>
            /// <returns>A new instance with the property reset to its default value.</returns>
            /// <exception cref="ArgumentNullException"><paramref name="instance"/> or <paramref name="propertyPicker"/> is <c>null</c>.</exception>
            /// <exception cref="InvalidOperationException">
            /// <paramref name="propertyPicker"/> does not select a property directly on the instance,
            /// or the property cannot be set via constructor or property setter.
            /// </exception>
            public T Without<TProperty>(Expression<Func<T, TProperty>> propertyPicker)
                => instance.With(propertyPicker, default(TProperty)!);

            /// <summary>
            /// Returns a copy of the instance with the selected property set to a value computed from its current value.
            /// </summary>
            /// <param name="propertyPicker">An expression selecting a property directly on the instance, e.g. <c>p =&gt; p.Age</c>.</param>
            /// <param name="propertyValueFactory">An expression computing the new value from the current value, e.g. <c>age =&gt; age + 1</c>.</param>
            /// <typeparam name="TProperty">The type of the property.</typeparam>
            /// <returns>A new instance with the property set to the computed value.</returns>
            /// <exception cref="ArgumentNullException">
            /// <paramref name="instance"/>, <paramref name="propertyPicker"/> or <paramref name="propertyValueFactory"/> is <c>null</c>.
            /// </exception>
            /// <exception cref="InvalidOperationException">
            /// <paramref name="propertyPicker"/> does not select a property directly on the instance,
            /// or the property cannot be set via constructor or property setter.
            /// </exception>
            public T With<TProperty>(
                Expression<Func<T, TProperty>> propertyPicker,
                Expression<Func<TProperty, TProperty>> propertyValueFactory
            )
            {
                if (instance is null) throw new ArgumentNullException(nameof(instance));
                if (propertyPicker is null) throw new ArgumentNullException(nameof(propertyPicker));
                if (propertyValueFactory is null) throw new ArgumentNullException(nameof(propertyValueFactory));

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

            /// <summary>
            /// Returns a copy of the instance with the selected property set to the given value.
            /// </summary>
            /// <remarks>
            /// Nullable reference type annotations are not enforced: a non-nullable reference property can be set to <c>null</c>.
            /// </remarks>
            /// <param name="propertyPicker">An expression selecting a property directly on the instance, e.g. <c>p =&gt; p.Name</c>.</param>
            /// <param name="propertyValue">The new value of the property.</param>
            /// <typeparam name="TProperty">The type of the property.</typeparam>
            /// <returns>A new instance with the property set to <paramref name="propertyValue"/>.</returns>
            /// <exception cref="ArgumentNullException"><paramref name="instance"/> or <paramref name="propertyPicker"/> is <c>null</c>.</exception>
            /// <exception cref="InvalidOperationException">
            /// <paramref name="propertyPicker"/> does not select a property directly on the instance,
            /// or the property cannot be set via constructor or property setter.
            /// </exception>
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