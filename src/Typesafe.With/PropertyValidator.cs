﻿using System;
using System.Linq;
using System.Reflection;
using Typesafe.Kernel;

namespace Typesafe.With
{
    internal static class PropertyValidator
    {
        public static void Validate<T>(string propertyName)
        {
            // Can we set the property via constructor?
            var hasConstructorParameter = HasConstructorParameter<T>(propertyName);
            
            if (hasConstructorParameter) return;
            
            // Can we set the property via property setter?
            var hasPropertySetter = HasPropertySetter<T>(propertyName);
            
            if (hasPropertySetter) return;

            // If we cannot do either, then there is no point in continuing.
            throw new InvalidOperationException($"Property '{propertyName.ToPropertyCase()}' cannot be set via constructor or property setter.");
        }

        private static bool HasPropertySetter<T>(string propertyName)
        {
            return TypeUtils.GetPropertyDictionary<T>().TryGetValue(propertyName, out var propertyInfo) && propertyInfo.CanWrite;
        }

        private static bool HasConstructorParameter<T>(string propertyName)
        {
            var constructorParameters = typeof(T)
                                            ?.GetConstructors()
                                            ?.OrderByDescending(info => info.GetParameters().Length)
                                            ?.FirstOrDefault()
                                            ?.GetParameters()
                                        ?? new ParameterInfo[0];
            
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
}