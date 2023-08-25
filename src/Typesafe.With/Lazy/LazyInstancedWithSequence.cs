using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Typesafe.With.Sequence;

namespace Typesafe.With.Lazy
{
  public class LazyInstancedWithSequence<T> where T : class
  {
    private readonly Dictionary<string, object> _properties;
    private readonly T _instance;

    internal LazyInstancedWithSequence(T instance)
      : this(instance, new Dictionary<string, object>())
    {
    }

    private LazyInstancedWithSequence(T instance, Dictionary<string, object> properties)
    {
      _instance = instance ?? throw new ArgumentNullException(nameof(instance));
      _properties = properties ?? throw new ArgumentNullException(nameof(properties));
    }

    /// <summary>
    /// Adds the mutation to the sequence.
    /// </summary>
    /// <param name="propertyPicker">An expression representing the property to update.</param>
    /// <param name="propertyValue">The value to set the property to.</param>
    /// <typeparam name="TProperty">The type of the property.</typeparam>
    /// <returns>The sequence.</returns>
    /// <exception cref="ArgumentNullException">If <paramref name="propertyPicker"/> is null.</exception>
    public LazyInstancedWithSequence<T> With<TProperty>(
      Expression<Func<T, TProperty>> propertyPicker,
      TProperty propertyValue
    )
    {
      if (propertyPicker == null) throw new ArgumentNullException(nameof(propertyPicker));
      
      var propertyName = propertyPicker.GetPropertyName();
      var dictionary = new Dictionary<string, object>(_properties).AddOrUpdate(propertyName, propertyValue);
      
      return new LazyInstancedWithSequence<T>(_instance, dictionary);
    }

    /// <summary>
    /// Adds the mutation to the sequence.
    /// </summary>
    /// <param name="propertyPicker">An expression representing the property to update.</param>
    /// <param name="propertyValueFactory">A function taking in the current value and returning the new value.</param>
    /// <typeparam name="TProperty">The type of the property.</typeparam>
    /// <returns>The sequence.</returns>
    /// <exception cref="ArgumentNullException">If either parameter is null.</exception>
    public LazyInstancedWithSequence<T> With<TProperty>(
      Expression<Func<T, TProperty>> propertyPicker,
      Func<TProperty> propertyValueFactory
    )
    {
      if (propertyPicker == null) throw new ArgumentNullException(nameof(propertyPicker));
      if (propertyValueFactory == null) throw new ArgumentNullException(nameof(propertyValueFactory));

      var propertyName = propertyPicker.GetPropertyName();
      var valueFactory = new ValueFactory<TProperty>(propertyValueFactory);
      var dictionary = new Dictionary<string, object>(_properties).AddOrUpdate(propertyName, valueFactory);

      return new LazyInstancedWithSequence<T>(_instance, dictionary);
    }

    /// <summary>
    /// Applies the sequence to produce an updated instance of <typeparamref name="T"/>.
    /// </summary>
    /// <returns>An instance of <typeparamref name="T"/> with the sequence applied.</returns>
    public T Apply()
    {
      var resolvedProperties = ResolveLazyPropertyValues();
      var sequence = new WithSequence<T>(resolvedProperties);
      var appliedInstance = sequence.ApplyTo(_instance);

      return appliedInstance;
    }

    private Dictionary<string, object> ResolveLazyPropertyValues()
    {
      return _properties.UpdateValues(PropertyValueResolver.Resolve);
    }

    public static implicit operator T(LazyInstancedWithSequence<T> builder) => builder.Apply();
  }
}