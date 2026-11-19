using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Typesafe.With
{
    internal static class TypeUtils
    {
        extension<T>(T instance) where T : notnull
        {
            public Dictionary<string, PropertyInfo> PropertyDictionary =>
                instance.CorrectedType
                    .GetProperties()
                    .ToDictionary(info => info.Name.ToParameterCase());

            public ConstructorInfo GetSuitableConstructor() =>
                instance.CorrectedType
                    .GetConstructors()
                    .OrderByDescending(info => info.GetParameters().Length)
                    .FirstOrDefault()
                ?? throw new InvalidOperationException($"Could not find any constructor for type {typeof(T)}.");

            private Type CorrectedType => typeof(T).IsInterface ? instance.GetType() : typeof(T);
        }
    }
}