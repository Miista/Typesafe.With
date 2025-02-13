using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Mono.Reflection;

namespace Typesafe.With
{
    class ExpressionTreeWithBuilder
    {
        private static readonly MethodInfo WithMethod = typeof(ExpressionTreeWithBuilder).GetMethod(nameof(With)) ?? throw new Exception($"Method {nameof(With)} not found");

        public static Func<T, T> With<T>(params Replacement[] replacements)
        {
            var instanceType = typeof(T);
            
            // Parameters
            var incomingInstance = Expression.Parameter(instanceType);
            
            // Variables
            var instanceVariable = Expression.Variable(instanceType);

            if (replacements.Length == 0)
            {
                return Expression.Lambda<Func<T, T>>(
                        body: Expression.Block(
                            variables: new[] { instanceVariable },
                            expressions: Expression.Assign(instanceVariable, incomingInstance)
                        ),
                        parameters: incomingInstance
                    )
                    .Compile();
            }

            var body = new Stack<Expression>();
            var nestedReplacementsByMember = replacements
                .Where(r => r.IsNested)
                .ToLookup(r => r.Member);
                
            var replacementsByProperty = replacements
                .GroupBy(r => r.Property)
                // If there are more than one, we just need the most recent
                .Select(g => g.Last())
                .ToDictionary(g => g.Property);

            // 1. Construct instance of T (and set properties via constructor)
            var ctor = TypeUtils.GetSuitableConstructor1<T>();

            var parameterInfoMap = CreateParameterInfoMap(ctor);
            var constructorArguments = ctor.GetParameters()
                .Select(p => parameterInfoMap[p])
                .Select(GetReplacementExpression)
                .ToArray();

            // Instantiate the object
            body.Push(Expression.Assign(instanceVariable, Expression.New(ctor, constructorArguments)));

            // 2+3. Set new properties via property setters + Copy remaining properties
            var properties = instanceType
                .GetProperties()
                .Where(p => p.CanWrite)
                // Remove properties set via constructor
                .Except(parameterInfoMap.Values);
            foreach (var propertyInfo in properties)
            {
                var propertyExpression = Expression.Property(instanceVariable, propertyInfo);

                Expression newValue;
                
                var nestedReplacements = nestedReplacementsByMember[propertyInfo].ToArray();

                if (nestedReplacements.Any())
                {
                    var liftedReplacements = nestedReplacements
                        .Select(r => r.Lift())
                        .ToArray();
                    var method = WithMethod.MakeGenericMethod(propertyInfo.PropertyType);
                    var methodCallExpression = Expression.Call(null, method, arguments: new []{ Expression.Constant(liftedReplacements) });

                    var funcT2T = Expression.Invoke(Expression.Lambda(methodCallExpression));
                    var invokeFuncT2TWithProperty = Expression.Invoke(funcT2T, propertyExpression);
                    var invokeFuncT2TWithInstance = Expression.Lambda(invokeFuncT2TWithProperty, instanceVariable);
                    newValue = Expression.Invoke(invokeFuncT2TWithInstance, incomingInstance);
                }
                else
                {
                    newValue = GetReplacementExpression(propertyInfo);
                }
                
                // Assign property
                body.Push(Expression.Assign(propertyExpression, newValue));
            }

            // Load the new instance on the stack
            body.Push(instanceVariable);

            var lambda = Expression.Lambda<Func<T, T>>(
                body: Expression.Block(
                    variables: new[] { instanceVariable },
                    expressions: body.Reverse()
                ),
                parameters: incomingInstance
            );

            // Compile the expression tree
            return lambda.Compile();

            Expression GetReplacementExpression(PropertyInfo propertyInfo)
            {
                var foundValue = replacementsByProperty.TryGetValue(propertyInfo, out var replacement);
                var matchingReplacement = replacementsByProperty.SingleOrDefault(r => r.Key.MetadataToken == propertyInfo.MetadataToken).Value;

                if (!foundValue)
                {
                    // If we cannot find a replacement, we just use the existing value
                    return Expression.Property(incomingInstance, propertyInfo);
                }

                // If the replacement is a factory, we need to invoke it
                if (replacement.IsFactory)
                {
                    var func = replacement.Value as Delegate;
                    return Expression.Invoke(
                        expression: Expression.Constant(func),
                        arguments: Expression.Property(incomingInstance, propertyInfo)
                    );
                }

                // Otherwise, we just use the value
                return Expression.Constant(replacement.Value, replacement.Property.PropertyType);
            }
        }
        
        public static Dictionary<ParameterInfo, PropertyInfo> CreateParameterInfoMap(ConstructorInfo constructor, PropertyInfo[] properties = null)
        {
            var map = new Dictionary<ParameterInfo, PropertyInfo>();

            var declaringType = constructor.DeclaringType ?? throw new Exception($"Method {constructor.Name} does not have a {nameof(ConstructorInfo.DeclaringType)}");

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
                                var matchingParameter = parameterInfos.SingleOrDefault(p => p.ParameterType == pair.Key.ParameterType && p.Name == pair.Key.Name);
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