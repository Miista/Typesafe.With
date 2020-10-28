﻿using System;

namespace TypeMerger
{
    public struct PropertyValue
    {
        public string PropertyName { get; }
        public object Value { get; }

        public PropertyValue(string propertyName, object value)
        {
            PropertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
            Value = value;
        }
    }
}