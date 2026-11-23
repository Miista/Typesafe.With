using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace Typesafe.With;

/// <summary>
/// Extension methods for creating modified copies of every element in a sequence in a type-safe manner.
/// </summary>
/// <remarks>
/// Each element is copied using the same rules as <see cref="ObjectExtensions"/>; the original elements are never modified.
/// The result is evaluated lazily.
/// </remarks>
public static class EnumerableExtensions
{
    /// <typeparam name="T">The type of the elements.</typeparam>
    /// <param name="collection">The sequence whose elements to copy.</param>
    extension<T>(IEnumerable<T> collection)
        where T : notnull
    {
        /// <summary>
        /// Returns a sequence of copies of the elements, each with the selected property set to the given value.
        /// </summary>
        /// <param name="propertyPicker">An expression selecting a property directly on the element, e.g. <c>p =&gt; p.Name</c>.</param>
        /// <param name="propertyValue">The new value of the property.</param>
        /// <typeparam name="TProperty">The type of the property.</typeparam>
        /// <returns>A lazily evaluated sequence of new instances with the property set to <paramref name="propertyValue"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="collection"/> or <paramref name="propertyPicker"/> is <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown on enumeration if <paramref name="propertyPicker"/> does not select a property directly on the element,
        /// or the property cannot be set via constructor or property setter.
        /// </exception>
        public IEnumerable<T> WithAll<TProperty>(Expression<Func<T, TProperty>> propertyPicker, TProperty propertyValue)
        {
            if (collection is null) throw new ArgumentNullException(nameof(collection));
            if (propertyPicker is null) throw new ArgumentNullException(nameof(propertyPicker));

            return collection.Select(e => e.With(propertyPicker, propertyValue));
        }

        /// <summary>
        /// Returns a sequence of copies of the elements, each with the selected property set to a value computed from its current value.
        /// </summary>
        /// <param name="propertyPicker">An expression selecting a property directly on the element, e.g. <c>p =&gt; p.Age</c>.</param>
        /// <param name="propertyFactory">An expression computing the new value from the current value, e.g. <c>age =&gt; age + 1</c>.</param>
        /// <typeparam name="TProperty">The type of the property.</typeparam>
        /// <returns>A lazily evaluated sequence of new instances with the property set to the computed value.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="collection"/>, <paramref name="propertyPicker"/> or <paramref name="propertyFactory"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown on enumeration if <paramref name="propertyPicker"/> does not select a property directly on the element,
        /// or the property cannot be set via constructor or property setter.
        /// </exception>
        public IEnumerable<T> WithAll<TProperty>(
            Expression<Func<T, TProperty>> propertyPicker,
            Expression<Func<TProperty, TProperty>> propertyFactory
        )
        {
            if (collection is null) throw new ArgumentNullException(nameof(collection));
            if (propertyPicker is null) throw new ArgumentNullException(nameof(propertyPicker));
            if (propertyFactory is null) throw new ArgumentNullException(nameof(propertyFactory));

            return collection.Select(e => e.With(propertyPicker, propertyFactory));
        }
    }
}
