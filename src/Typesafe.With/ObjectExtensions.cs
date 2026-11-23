using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Typesafe.With;

/// <summary>
/// Extension methods for creating modified copies of objects in a type-safe manner.
/// </summary>
/// <remarks>
/// The original instance is never modified. A new instance is created via the public constructor with the most parameters,
/// and remaining properties are set via property setters or copied from the original instance.
/// For a nested property such as <c>p =&gt; p.Address.City</c>, every object on the path is copied this way;
/// objects off the path are shared with the original.
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
        /// <param name="propertyPicker">An expression selecting a property on the instance, optionally nested, e.g. <c>p =&gt; p.Name</c> or <c>p =&gt; p.Address.City</c>.</param>
        /// <typeparam name="TProperty">The type of the property.</typeparam>
        /// <returns>A new instance with the property reset to its default value.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="instance"/> or <paramref name="propertyPicker"/> is <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException">
        /// <paramref name="propertyPicker"/> does not select a chain of properties starting at the instance,
        /// a property on the path is <c>null</c>, or a property on the path cannot be set via constructor or property setter.
        /// </exception>
        public T Without<TProperty>(Expression<Func<T, TProperty>> propertyPicker)
            => instance.With(propertyPicker, default(TProperty)!);

        /// <summary>
        /// Returns a copy of the instance with the selected property set to a value computed from its current value.
        /// </summary>
        /// <param name="propertyPicker">An expression selecting a property on the instance, optionally nested, e.g. <c>p =&gt; p.Age</c> or <c>p =&gt; p.Owner.Age</c>.</param>
        /// <param name="propertyValueFactory">An expression computing the new value from the current value, e.g. <c>age =&gt; age + 1</c>.</param>
        /// <typeparam name="TProperty">The type of the property.</typeparam>
        /// <returns>A new instance with the property set to the computed value.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="instance"/>, <paramref name="propertyPicker"/> or <paramref name="propertyValueFactory"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// <paramref name="propertyPicker"/> does not select a chain of properties starting at the instance,
        /// a property on the path is <c>null</c>, or a property on the path cannot be set via constructor or property setter.
        /// </exception>
        public T With<TProperty>(
            Expression<Func<T, TProperty>> propertyPicker,
            Expression<Func<TProperty, TProperty>> propertyValueFactory
        )
        {
            if (instance is null) throw new ArgumentNullException(nameof(instance));
            if (propertyPicker is null) throw new ArgumentNullException(nameof(propertyPicker));
            if (propertyValueFactory is null) throw new ArgumentNullException(nameof(propertyValueFactory));

            return instance.WithPath(propertyPicker.PropertyPath, 0, new DependentValue(propertyValueFactory));
        }

        /// <summary>
        /// Returns a copy of the instance with the selected property set to the given value.
        /// </summary>
        /// <remarks>
        /// Nullable reference type annotations are not enforced: a non-nullable reference property can be set to <c>null</c>.
        /// </remarks>
        /// <param name="propertyPicker">An expression selecting a property on the instance, optionally nested, e.g. <c>p =&gt; p.Name</c> or <c>p =&gt; p.Address.City</c>.</param>
        /// <param name="propertyValue">The new value of the property.</param>
        /// <typeparam name="TProperty">The type of the property.</typeparam>
        /// <returns>A new instance with the property set to <paramref name="propertyValue"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="instance"/> or <paramref name="propertyPicker"/> is <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException">
        /// <paramref name="propertyPicker"/> does not select a chain of properties starting at the instance,
        /// a property on the path is <c>null</c>, or a property on the path cannot be set via constructor or property setter.
        /// </exception>
        public T With<TProperty>(
            Expression<Func<T, TProperty>> propertyPicker,
            TProperty propertyValue)
        {
            if (instance is null) throw new ArgumentNullException(nameof(instance));
            if (propertyPicker is null) throw new ArgumentNullException(nameof(propertyPicker));

            return instance.WithPath(propertyPicker.PropertyPath, 0, propertyValue);
        }

        /// <summary>
        /// Copies the instance with <c>path[index]</c> set. For a nested path, the property's current value is
        /// copied recursively first, and the copy is assigned to the property.
        /// </summary>
        /// <param name="path">The properties from the instance to the property to set.</param>
        /// <param name="index">The position in <paramref name="path"/> of the property on this instance.</param>
        /// <param name="leafValue">The new value of the last property in the path, or a <see cref="DependentValue"/>.</param>
        private T WithPath(IReadOnlyList<PropertyInfo> path, int index, object? leafValue)
        {
            var property = path[index];
            var propertyName = property.Name.ToParameterCase();

            if (index == path.Count - 1) return instance.WithProperty(propertyName, leafValue);

            var nestedInstance = property.GetValue(instance) ?? throw new InvalidOperationException(
                $"Error calling {nameof(With)} on type {typeof(T)}: Property '{property.Name}' is null, so its nested property '{path[index + 1].Name}' cannot be set."
            );

            var nestedCopy = GetWithPathDelegate(property.PropertyType)(nestedInstance, path, index + 1, leafValue);

            return instance.WithProperty(propertyName, nestedCopy);
        }

        private T WithProperty(string propertyName, object? value)
        {
            var properties = new Dictionary<string, object?>
            {
                {propertyName, value}
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

    private delegate object WithPathDelegate(object instance, IReadOnlyList<PropertyInfo> path, int index, object? leafValue);

    private static readonly ConcurrentDictionary<Type, WithPathDelegate> WithPathDelegates = new();

    /// <summary>
    /// Gets a cached delegate calling <c>WithPath</c> with <c>T</c> set to <paramref name="type"/>,
    /// so nested instances are copied with the same rules as the root.
    /// </summary>
    private static WithPathDelegate GetWithPathDelegate(Type type) =>
        WithPathDelegates.GetOrAdd(
            type,
            t => (WithPathDelegate) typeof(ObjectExtensions)
                .GetMethod(nameof(WithPathUntyped), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(t)
                .CreateDelegate(typeof(WithPathDelegate))
        );

    private static object WithPathUntyped<TInstance>(object instance, IReadOnlyList<PropertyInfo> path, int index, object? leafValue)
        where TInstance : notnull
        => ((TInstance) instance).WithPath(path, index, leafValue);
}