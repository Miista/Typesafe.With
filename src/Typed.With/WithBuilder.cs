﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace TypeWither
{
    public readonly struct WithBuilder<T>
    {
        private readonly T _instance;
        private readonly HashSet<PropertyValue> _properties;

        internal WithBuilder(T instance)
        {
            _instance = instance;
            _properties = new HashSet<PropertyValue>();
        }

        public WithBuilder<T> With<TValue>(Expression<Func<T, TValue>> selector, TValue value)
        {
            if (selector == null) throw new ArgumentNullException(nameof(selector));
            
            var propertyInfo = GetPropertyName(selector);

            var propertyValue = new PropertyValue(propertyInfo.Name, value);
            _properties.Add(propertyValue);

            return this;
        }

        private static PropertyInfo GetPropertyName<TValue>(Expression<Func<T, TValue>> expression)
        {
            switch (expression.Body)
            {
                case UnaryExpression u when u.Operand is MemberExpression um:
                    return um.Member as PropertyInfo;
                case MemberExpression m:
                    return m.Member as PropertyInfo;
                default:
                    throw new InvalidOperationException($"Cannot retrieve property from expression '{expression}'");
            }
        }

        private T Build()
        {
            return TypeWither.With(_instance, _properties.ToArray());
        }

        public static implicit operator T(WithBuilder<T> builder) => builder.Build();
    }
}