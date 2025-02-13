using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Typesafe.With
{
    public class Wither<T>
    {
        private readonly T _instance;
        private readonly List<Replacement> _replacements = new List<Replacement>();

        public Wither(T instance)
        {
            _instance = instance;
        }

        public Wither<T> With<TProperty>(Expression<Func<T, TProperty>> propertyPicker, TProperty value)
        {
            _replacements.Add(ReplacementHelper.Create(propertyPicker, value));
            return this;
        }
        
        public Wither<T> With<TProperty>(Expression<Func<T, TProperty>> propertyPicker, Func<TProperty, TProperty> valueFactory)
        {
            _replacements.Add(ReplacementHelper.Create(propertyPicker, valueFactory));
            return this;
        }

        internal T Build()
        {
            // var correctedType = TypeUtils.GetCorrectedType(_instance);
            return ExpressionTreeWithBuilder.With<T>(_replacements.ToArray())(_instance);
        }

        public static implicit operator T(Wither<T> wither) => wither.Build();
    }
}