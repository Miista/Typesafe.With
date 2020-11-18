﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Typesafe.Utils;

namespace Typesafe.Builders
{
    /// <summary>
    /// An IWithBuilder which first constructs the instance using the constructor.
    /// Next, any parameter not initialized by the constructor is set using the property's setter (if available).
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class MixedConstructorAndPropertyWithBuilder<T> : ConstructorWithBuilder<T>, IWithBuilder<T>
    {
        private readonly ConstructorInfo _constructorInfo;

        public MixedConstructorAndPropertyWithBuilder(ConstructorInfo constructorInfo)
            : base(constructorInfo)
        {
            _constructorInfo = constructorInfo ?? throw new ArgumentNullException(nameof(constructorInfo));
        }

        public new T Construct(T instance, IReadOnlyDictionary<string, object> properties)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            if (properties == null) throw new ArgumentNullException(nameof(properties));
            
            // Set properties via constructor
            var constructedInstance = base.Construct(instance, properties);
            
            // Set properties via property setters
            var remainingProperties = GetParametersNotInConstructor(properties);
            var enrichedInstance = EnrichByProperty(constructedInstance, remainingProperties);

            // Copy remaining properties from source to destination
            var publicProperties = GetPropertiesToBeSet(properties);
            var copyProperties = publicProperties.Values;
            var enrichedInstanceWithCopiedProperties = CopyProperties(instance, enrichedInstance, copyProperties);

            return enrichedInstanceWithCopiedProperties;
        }

        private static T CopyProperties(T source, T destination, IEnumerable<PropertyInfo> properties)
        {
            foreach (var kvp in properties)
            {
                var value = kvp.GetValue(source);
                kvp.SetValue(destination, value);
            }

            return destination;
        }

        private IReadOnlyDictionary<string, PropertyInfo> GetPropertiesToBeSet(
            IReadOnlyDictionary<string, object> properties)
        {
            var publicProperties = TypeUtils.GetPropertyDictionary<T>();
            foreach (var kvp in properties)
            {
                publicProperties.Remove(kvp.Key);
            }
            
            foreach (var constructorParameter in ConstructorParameters)
            {
                publicProperties.Remove(constructorParameter);
            }

            return publicProperties;
        }

        private IEnumerable<string> ConstructorParameters
        {
            get
            {
                return _constructorInfo
                    .GetParameters()
                    .Select(parameterInfo => parameterInfo.Name)
                    .Select(UppercaseFirstLetter)
                    .ToList();
            }
        }

        private IReadOnlyDictionary<string, object> GetParametersNotInConstructor(IReadOnlyDictionary<string, object> properties)
        {
            var constructorParameters = ConstructorParameters
                .ToLookup(parameterName => parameterName);

            var propertiesCoveredByConstructor = properties
                .Where(kvp => constructorParameters.Contains(kvp.Key));
                
            return properties
                .Except(propertiesCoveredByConstructor)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
        }
        
        private static string UppercaseFirstLetter(string s)
        {
            var firstLetter = s[0];
            var firstLetterInUppercase = char.ToUpperInvariant(firstLetter);
            var remainingString = s.Substring(1);
            
            return firstLetterInUppercase + remainingString;
        }
        
        private static TInstance EnrichByProperty<TInstance>(
            TInstance instance,
            IReadOnlyDictionary<string, object> newProperties)
        {
            var existingProperties = TypeUtils.GetPropertyDictionary<TInstance>();

            foreach (var property in newProperties)
            {
                if (!existingProperties.TryGetValue(property.Key, out var existingProperty))
                {
                    throw new InvalidOperationException($"Cannot find property with name '{property.Key}'.");
                }

                if (!existingProperty.CanWrite)
                {
                    throw new InvalidOperationException($"Property '{property.Key}' cannot be written to.");
                }
                
                existingProperty.SetValue(instance, property.Value);
            }

            return instance;
        }
    }
}