using System;
using System.Collections.Generic;
using System.Reflection;

namespace Typesafe.With
{
    internal struct Replacement
    {
        public PropertyInfo Property { get; }
        public object Value { get; }
        public Stack<MemberInfo> Path { get; }

        public bool IsFactory
        {
            get
            {
                if (Value == null) return false;
                
                var type = Value.GetType();
                
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Func<,>))
                {
                    var genericArguments = type.GetGenericArguments();
                    return genericArguments[0] == Property.PropertyType && genericArguments[1] == Property.PropertyType;
                }

                return false;
            }
        }
        
        public bool IsNested => Path?.Count > 0;
        public MemberInfo Member => IsNested ? Path.Peek() : null;

        public Replacement(PropertyInfo property, object value, Stack<MemberInfo> path)
        {
            Property = property ?? throw new ArgumentNullException(nameof(property));
            Value = value;
            Path = path;
        }

        public Replacement Lift()
        {
            Path.Pop();
            
            return new Replacement(Property, Value, Path);
        }
    }
}