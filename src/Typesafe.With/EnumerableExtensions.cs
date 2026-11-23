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
    /// <param name="source">The sequence whose elements to copy.</param>
    extension<T>(IEnumerable<T> source)
        where T : notnull
    {
        /// <summary>
        /// Returns a sequence of copies of the elements, each with the selected property set to the given value.
        /// </summary>
        /// <param name="propertyPicker">An expression selecting a property on the element, optionally nested, e.g. <c>p =&gt; p.Name</c> or <c>p =&gt; p.Address.City</c>.</param>
        /// <param name="propertyValue">The new value of the property.</param>
        /// <typeparam name="TProperty">The type of the property.</typeparam>
        /// <returns>A lazily evaluated sequence of new instances with the property set to <paramref name="propertyValue"/>.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="propertyPicker"/> is <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown on enumeration if <paramref name="propertyPicker"/> does not select a chain of properties starting at the element,
        /// a property on the path is <c>null</c>, or a property on the path cannot be set via constructor or property setter.
        /// </exception>
        public IEnumerable<T> WithAll<TProperty>(Expression<Func<T, TProperty>> propertyPicker, TProperty propertyValue)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));
            if (propertyPicker is null) throw new ArgumentNullException(nameof(propertyPicker));

            return source.Select(e => e.With(propertyPicker, propertyValue));
        }

        /// <summary>
        /// Returns a sequence of copies of the elements, each with the selected property set to a value computed from its current value.
        /// </summary>
        /// <param name="propertyPicker">An expression selecting a property on the element, optionally nested, e.g. <c>p =&gt; p.Age</c> or <c>p =&gt; p.Owner.Age</c>.</param>
        /// <param name="propertyFactory">An expression computing the new value from the current value, e.g. <c>age =&gt; age + 1</c>.</param>
        /// <typeparam name="TProperty">The type of the property.</typeparam>
        /// <returns>A lazily evaluated sequence of new instances with the property set to the computed value.</returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="source"/>, <paramref name="propertyPicker"/> or <paramref name="propertyFactory"/> is <c>null</c>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown on enumeration if <paramref name="propertyPicker"/> does not select a chain of properties starting at the element,
        /// a property on the path is <c>null</c>, or a property on the path cannot be set via constructor or property setter.
        /// </exception>
        public IEnumerable<T> WithAll<TProperty>(
            Expression<Func<T, TProperty>> propertyPicker,
            Expression<Func<TProperty, TProperty>> propertyFactory
        )
        {
            if (source is null) throw new ArgumentNullException(nameof(source));
            if (propertyPicker is null) throw new ArgumentNullException(nameof(propertyPicker));
            if (propertyFactory is null) throw new ArgumentNullException(nameof(propertyFactory));

            return source.Select(e => e.With(propertyPicker, propertyFactory));
        }
    }
}
