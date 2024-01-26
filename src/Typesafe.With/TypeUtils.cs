using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Typesafe.With
{
    internal static class TypeUtils
    {
        public static Dictionary<string, PropertyInfo> GetPropertyDictionary<T>(T instance) =>
            GetCorrectedType(instance)
                .GetProperties()
                .ToDictionary(info => info.Name.ToParameterCase());

        public static ConstructorInfo GetSuitableConstructor<T>(T instance) =>
            GetCorrectedType(instance)
                .GetConstructors()
                .OrderByDescending(info => info.GetParameters().Length)
                .FirstOrDefault()
            ?? throw new InvalidOperationException($"Could not find any constructor for type {typeof(T)}.");

        private static Type GetCorrectedType<T>(T instance) =>
            typeof(T).IsInterface
                ? instance.GetType()
                : typeof(T);
    }
}