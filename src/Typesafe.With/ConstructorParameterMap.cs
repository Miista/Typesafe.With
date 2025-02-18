using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Typesafe.With
{
    internal class ConstructorParameterMap : Dictionary<PropertyInfo, PropertyInfo>, IDictionary<PropertyInfo, PropertyInfo>
    {
        private readonly Type _type;

        public ConstructorParameterMap(Dictionary<PropertyInfo, PropertyInfo> values, Type type) : base(values, new ConstructorHelper.PropertyMetadataTokenEqualityComparer())
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            
            _type = type ?? throw new ArgumentNullException(nameof(type));
        }

        public new bool TryGetValue(PropertyInfo key, out PropertyInfo value)
        {
            if (base.TryGetValue(key, out value))
            {
                return true;
            }

            // Resolve via interface mapping
            var interfaceMappings = _type.GetInterfaces().Select(i => _type.GetInterfaceMap(i)).ToArray();
            
            for (var i = 0; i < interfaceMappings.Length; i++)
            {
                var interfaceMapping = interfaceMappings[i];

                // We first find the get method in the interface
                var interfaceGetMethod = interfaceMapping.InterfaceMethods.FirstOrDefault(m => m.MetadataToken == key.GetMethod.MetadataToken);

                if (interfaceGetMethod == null) continue;

                // Next, we find the target method in the interface mapping
                var indexOfInterfaceMethod = Array.IndexOf(interfaceMappings[i].InterfaceMethods, interfaceGetMethod);
                var targetMethod = interfaceMappings[i].TargetMethods[indexOfInterfaceMethod];
                    
                if (PropertiesByGetMethod.TryGetValue(targetMethod, out var matchingProperty))
                {
                    value = matchingProperty;
                    return true;
                }
            }
            
            return false;
        }
        
        private Dictionary<MethodInfo, PropertyInfo> PropertiesByGetMethod => Keys.ToDictionary(p => p.GetMethod);
    }
    
}