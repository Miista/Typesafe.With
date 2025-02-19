using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Typesafe.With
{
    internal class ConstructorParameterMap : Dictionary<PropertyInfo, PropertyInfo>, IDictionary<PropertyInfo, PropertyInfo>
    {
        private readonly InterfaceMapping[] _interfaceMappings;

        private Dictionary<MethodInfo, PropertyInfo> PropertiesByGetMethod => Keys.ToDictionary(p => p.GetMethod);

        public ConstructorParameterMap(Dictionary<PropertyInfo, PropertyInfo> values, Type type) : base(values, new ConstructorHelper.PropertyMetadataTokenEqualityComparer())
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            if (type == null) throw new ArgumentNullException(nameof(type));

            _interfaceMappings = type.GetInterfaces().Select(type.GetInterfaceMap).ToArray();
        }

        public new bool TryGetValue(PropertyInfo key, out PropertyInfo value)
        {
            if (base.TryGetValue(key, out value)) return true;

            // Resolve via interface mapping
            foreach (var interfaceMapping in _interfaceMappings)
            {
                // We first find the get method in the interface
                var indexOfInterfaceMethod = Array.FindIndex(interfaceMapping.InterfaceMethods, m => m.MetadataToken == key.GetMethod.MetadataToken);
                
                if (indexOfInterfaceMethod == -1) continue; // We found nothing
                
                // Next, we find the target method in the interface mapping
                var targetMethod = interfaceMapping.TargetMethods[indexOfInterfaceMethod];
                
                // This assumes that every property has a get method.
                // This is reasonable because if the property does not have a get method, it cannot be called in With.
                if (PropertiesByGetMethod.TryGetValue(targetMethod, out var matchingProperty))
                {
                    value = matchingProperty;
                    return true;
                }
            }
            
            return false;
        }

        public new bool Remove(PropertyInfo key)
        {
            if (TryGetValue(key, out var actualKey))
            {
                return base.Remove(actualKey);
            }

            return false;
        }
    }
    
}