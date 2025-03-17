using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Typesafe.With.Tests")]
[assembly: InternalsVisibleTo("Typesafe.Sandbox")]

namespace Typesafe.With
{
    public static class ObjectExtensions
    {
        public static T With<T, TProperty>(
            this T instance,
            Expression<Func<T, TProperty>> propertyPicker,
            Expression<Func<TProperty, TProperty>> propertyValueFactory
        )
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            if (propertyPicker == null) throw new ArgumentNullException(nameof(propertyPicker));

            if (IsRecord<T>())
            {
                return new RecordWithBuilder<T>().Construct(instance, propertyPicker, propertyValueFactory);
            }
            
            var property = propertyPicker.GetProperty();
            var properties = new Dictionary<PropertyInfo, object>(new ConstructorHelper.PropertyMetadataTokenEqualityComparer())
            {
                {property, new DependentValue(propertyValueFactory)}
            };

            Validate(property, instance);
            
            var constructor = TypeUtils.GetSuitableConstructor(instance);
            var builder = new UnifiedWithBuilder<T>(constructor);
            
            return builder.Construct(instance, properties);
        }

        public static T With<T, TProperty>(
            this T instance,
            Expression<Func<T, TProperty>> propertyPicker,
            TProperty propertyValue)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            if (propertyPicker == null) throw new ArgumentNullException(nameof(propertyPicker));

            if (IsRecord<T>())
            {
                return new RecordWithBuilder<T>().Construct(instance, propertyPicker, propertyValue);
            }
            
            var property = propertyPicker.GetProperty();
            var properties = new Dictionary<PropertyInfo, object>(new ConstructorHelper.PropertyMetadataTokenEqualityComparer())
            {
                {property, propertyValue}
            };

            Validate(property, instance);
            
            var constructor = TypeUtils.GetSuitableConstructor(instance);
            var builder = new UnifiedWithBuilder<T>(constructor);
            
            return builder.Construct(instance, properties);
        }

        private static bool IsRecord<T>()
        {
            var type = typeof(T);
            
            /*
             * Records have the following criteria:
             * 1. Deconstruct method
             * 2. Implement IEquatable<T> where T is the record type
             * 3. Equality operators: == and !=
             * 4. PrintMembers method
             * 5. In case of record classes:
             * 5a. A compiler generated clone method
             * 5b. A compiler generated EqualityContract property
             */
            
            // 1. Deconstruct method
            var deconstructMethod = type.GetMethod("Deconstruct", BindingFlags.Instance | BindingFlags.Public);
            
            if (deconstructMethod == null) return false;
            
            if (deconstructMethod.GetCustomAttribute<CompilerGeneratedAttribute>() == null) return false;
            
            // 2. Implement IEquatable<T> where T is the record type
            var genericEquatableInterface = typeof(IEquatable<>).MakeGenericType(type);
            var equatableInterface = type.GetInterface(genericEquatableInterface.Name);
            
            if (equatableInterface == null) return false;
            
            // 3. Equality operators: == and !=
            var equalityOperator = type.GetMethod("op_Equality", BindingFlags.Static | BindingFlags.Public);
            var inequalityOperator = type.GetMethod("op_Inequality", BindingFlags.Static | BindingFlags.Public);
            
            if (equalityOperator == null || equalityOperator.GetParameters()[0].ParameterType != typeof(T) || equalityOperator.GetCustomAttribute<CompilerGeneratedAttribute>() == null) return false;
            
            if (inequalityOperator == null || inequalityOperator.GetParameters()[0].ParameterType != typeof(T) || inequalityOperator.GetCustomAttribute<CompilerGeneratedAttribute>() == null) return false;

            // 4. PrintMembers method
            var printMembersMethod = type.GetMethod("PrintMembers", BindingFlags.Instance | BindingFlags.NonPublic);
            
            if (printMembersMethod == null) return false;

            if (printMembersMethod.GetCustomAttribute<CompilerGeneratedAttribute>() == null) return false;
            
            // 5. In case of record classes:
            if (type.IsClass)
            {
                // 5a. A compiler generated clone method
                // Structs do not need the clone method. Simply assigning the struct to a new variable, creates a copy.
                var cloneMethod = type.GetMethod("<Clone>$", BindingFlags.Instance | BindingFlags.Public);

                if (cloneMethod == null) return false;

                // The <Clone>$ method should return the same type as the record class
                if (cloneMethod.ReturnType != typeof(T)) return false;

                if (cloneMethod.GetCustomAttribute<CompilerGeneratedAttribute>() == null) return false;
                
                // 5b. A compiler generated EqualityContract property
                var equalityContractProperty = type.GetProperty("EqualityContract", BindingFlags.Instance | BindingFlags.NonPublic);
                
                if (equalityContractProperty == null) return false;

                if (equalityContractProperty.GetCustomAttribute<CompilerGeneratedAttribute>() == null) return false;
            }

            return true;
        }
        
        private static void Validate<T>(PropertyInfo propertyName, T instance)
        {
            // Can we set the property via constructor?
            var hasConstructorParameter = HasConstructorParameter(propertyName, instance);
            
            if (hasConstructorParameter) return;
            
            // Can we set the property via property setter?
            var hasPropertySetter = HasPropertySetter(propertyName);
            
            if (hasPropertySetter) return;

            // If we cannot do either, then there is no point in continuing.
            throw new InvalidOperationException(
                $"Error calling {nameof(With)} on type {typeof(T)}: Property '{propertyName.Name}' cannot be set via constructor or property setter. You can fix this by making the property settable or adding it as a constructor parameter."
            );
        }

        private static bool HasPropertySetter(PropertyInfo propertyName)
        {
            return propertyName.CanWrite;
        }

        private static bool HasConstructorParameter<T>(PropertyInfo propertyName, T instance)
        {
            var constructor = TypeUtils.GetSuitableConstructor(instance);
            var property2ParameterMap = ConstructorHelper.CreatePropertyInfoMap(constructor);

            return property2ParameterMap.ContainsKey(propertyName);
        }
    }
}