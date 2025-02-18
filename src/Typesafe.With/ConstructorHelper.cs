using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Mono.Reflection;

namespace Typesafe.With
{
    internal static class ConstructorHelper
    {
        public static Dictionary<ParameterInfo, PropertyInfo> CreateParameterInfoMap(ConstructorInfo constructor, PropertyInfo[] properties = null)
        {
            var map = new Dictionary<ParameterInfo, PropertyInfo>();

            var declaringType = constructor.DeclaringType
                                ?? throw new Exception($"Method {constructor.Name} does not have a {nameof(ConstructorInfo.DeclaringType)}");

            var allProperties = properties ?? declaringType.GetProperties();
            var propertiesBySetMethod = allProperties
                .Where(p => p.CanWrite)
                .ToDictionary(p => p.SetMethod);

            var arg2Location = new Dictionary<int, int>();
            var parameterInfos = constructor.GetParameters();
            var instructions = constructor.GetInstructions();

            foreach (var instruction in instructions)
            {
                if (instruction.OpCode == OpCodes.Ldarg_0 || instruction.OpCode == OpCodes.Nop)
                {
                    continue;
                }

                if (instruction.OpCode.Name.StartsWith("ldloc"))
                {
                    int index = instruction.OpCode.Name switch
                    {
                        "ldloc.0" => 0,
                        "ldloc.1" => 1,
                        "ldloc.2" => 2,
                        "ldloc.3" => 3,
                        "ldloc.s" => Array.IndexOf(parameterInfos, parameterInfos.Single(p => p == (ParameterInfo)instruction.Operand))
                    };

                    var argIndex = arg2Location[index];
                    var param = parameterInfos[argIndex];
                    var nextInstruction = instruction.Next;

                    if (nextInstruction?.OpCode == OpCodes.Call)
                    {
                        // Is this a property setter?
                        if (nextInstruction.Operand is MethodInfo methodInfo)
                        {
                            if (propertiesBySetMethod.TryGetValue(methodInfo, out var property))
                            {
                                map.Add(param, property);
                            }

                            continue;
                        }

                        if (nextInstruction.Operand is ConstructorInfo parentConstructor)
                        {
                            var parameterInfoMap = CreateParameterInfoMap(parentConstructor, allProperties);
                        }
                    }

                    // Handle backing field
                    else if (nextInstruction?.OpCode == OpCodes.Stfld)
                    {
                        var backingFieldPattern = new Regex("<(.+)>k__BackingField", RegexOptions.Compiled);

                        if (nextInstruction.Operand is FieldInfo fieldInfo)
                        {
                            if (fieldInfo.GetCustomAttribute<CompilerGeneratedAttribute>() != null)
                            {
                                if (backingFieldPattern.IsMatch(fieldInfo.Name))
                                {
                                    var propertyName = backingFieldPattern.Match(fieldInfo.Name).Groups[1].Value;

                                    var property = allProperties.Single(p => string.Equals(p.Name, propertyName, StringComparison.Ordinal));

                                    map.Add(param, property);
                                }
                            }

                            continue;
                        }
                    }

                    else if (nextInstruction?.OpCode.Name.StartsWith("stloc") ?? false)
                    {
                        var locationIndex = nextInstruction.OpCode.Name switch
                        {
                            "stloc.0" => 0,
                            "stloc.1" => 1,
                            "stloc.2" => 2,
                            "stloc.3" => 3,
                            _ => throw new Exception("I don't know what to do")
                        };

                        arg2Location.Add(locationIndex, index);

                        Console.WriteLine("w");

                        continue;
                    }
                }

                if (instruction.OpCode.Name.StartsWith("ldarg"))
                {
                    int index = instruction.OpCode.Name switch
                    {
                        "ldarg.1" => 0,
                        "ldarg.2" => 1,
                        "ldarg.3" => 2,
                        "ldarg.s" => Array.IndexOf(parameterInfos, parameterInfos.Single(p => p == (ParameterInfo)instruction.Operand))
                    };

                    var param = parameterInfos[index];

                    var nextInstruction = instruction.Next;

                    if (nextInstruction?.OpCode == OpCodes.Call)
                    {
                        // Is this a property setter?
                        if (nextInstruction.Operand is MethodInfo methodInfo)
                        {
                            if (propertiesBySetMethod.TryGetValue(methodInfo, out var property))
                            {
                                map.Add(param, property);
                            }

                            continue;
                        }

                        if (nextInstruction.Operand is ConstructorInfo parentConstructor)
                        {
                            var parentParameterMap = CreateParameterInfoMap(parentConstructor, allProperties);

                            foreach (var pair in parentParameterMap)
                            {
                                var matchingParameter = parameterInfos.SingleOrDefault(
                                    p => p.ParameterType == pair.Key.ParameterType && p.Name == pair.Key.Name
                                );
                                map.Add(matchingParameter, pair.Value);
                            }
                        }
                    }

                    // Handle backing field
                    else if (nextInstruction?.OpCode == OpCodes.Stfld)
                    {
                        var backingFieldPattern = new Regex("<(.+)>k__BackingField", RegexOptions.Compiled);

                        if (nextInstruction.Operand is FieldInfo fieldInfo)
                        {
                            if (fieldInfo.GetCustomAttribute<CompilerGeneratedAttribute>() != null)
                            {
                                if (backingFieldPattern.IsMatch(fieldInfo.Name))
                                {
                                    var propertyName = backingFieldPattern.Match(fieldInfo.Name).Groups[1].Value;

                                    var property = allProperties.Single(p => p.Name == propertyName);

                                    map.Add(param, property);
                                }
                            }

                            continue;
                        }
                    }

                    else if (nextInstruction?.OpCode.Name.StartsWith("stloc") ?? false)
                    {
                        var locationIndex = nextInstruction.OpCode.Name switch
                        {
                            "stloc.0" => 0,
                            "stloc.1" => 1,
                            "stloc.2" => 2,
                            "stloc.3" => 3,
                            _ => throw new Exception("I don't know what to do")
                        };

                        arg2Location.Add(locationIndex, index);

                        Console.WriteLine("w");

                        continue;
                    }

                    // throw new Exception("I don't know what to do");
                }
            }

            return map;
        }
    }
}