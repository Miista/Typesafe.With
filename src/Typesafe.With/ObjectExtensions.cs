using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Typesafe.With
{
    public static class ObjectExtensions
    {
        private static readonly MethodInfo WithMethod = typeof(ObjectExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m =>
                m.Name == nameof(With)
                && m.GetParameters()[1].ParameterType.GetGenericTypeDefinition() == typeof(Expression<>)
                && m.GetParameters()[2] .ParameterType.GetGenericTypeDefinition() == typeof(Expression<>)
            ) ?? throw new Exception();

        public static T With<T, TProperty>(
            this T instance,
            Expression<Func<T, TProperty>> propertyPicker,
            Expression<Func<TProperty, TProperty>> propertyValueFactory
        )
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            if (propertyPicker == null) throw new ArgumentNullException(nameof(propertyPicker));

            var propertyName = propertyPicker.GetPropertyName();
            var properties = new Dictionary<string, object> { { propertyName, new DependentValue(propertyValueFactory) } };

            Validate(propertyName, instance);

            var constructor = TypeUtils.GetSuitableConstructor(instance);
            var builder = new UnifiedWithBuilder<T>(constructor);

            return builder.Construct(instance, properties);
        }

        public static T With<T, TProperty>(
            this T instance,
            Expression<Func<T, TProperty>> propertyPicker,
            TProperty propertyValue
        )
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            if (propertyPicker == null) throw new ArgumentNullException(nameof(propertyPicker));

            var propertyName = propertyPicker.GetPropertyName();
            var properties = new Dictionary<string, object> { { propertyName, propertyValue } };

            Validate(propertyName, instance);

            var constructor = TypeUtils.GetSuitableConstructor(instance);
            var builder = new UnifiedWithBuilder<T>(constructor);

            return builder.Construct(instance, properties);
        }

        public static T NestedWith<T, TProperty>(
            this T instance,
            Expression<Func<T, TProperty>> propertyPicker,
            TProperty propertyValue
        )
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            if (propertyPicker == null) throw new ArgumentNullException(nameof(propertyPicker));

            var withExpression = NestedWithQueue1(propertyPicker, _ => propertyValue);
            return withExpression.Compile().Invoke(instance);
        }
        
        public static T NestedWith<T, TProperty>(
            this T instance,
            Expression<Func<T, TProperty>> propertyPicker,
            Expression<Func<TProperty, TProperty>> propertyValueFactory
        )
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            if (propertyPicker == null) throw new ArgumentNullException(nameof(propertyPicker));

            var withExpression = NestedWithQueue1(propertyPicker, propertyValueFactory);
            return withExpression.Compile().Invoke(instance);
        }

        private static Expression<Func<T, T>> NestedWithQueue1<T, TValue>(Expression<Func<T, TValue>> picker, Expression<Func<TValue, TValue>> value)
        {
            var members = GetMembers();

            var root = members.Dequeue();

            var rootExpression = BuildLambda(root, 0, value);

            for (var i = 1; i < 1 + members.Count; i++)
            {
                var current1 = members.Dequeue();

                var lambdaExpression = BuildLambda(current1, i, rootExpression);
                rootExpression = lambdaExpression;
            }

            return rootExpression as Expression<Func<T, T>>;

            LambdaExpression BuildLambda(MemberExpression current, int i, Expression propertyValue)
            {
                var genericWithMethod = WithMethod.MakeGenericMethod(current.Expression.Type, current.Type);
                var memberAccessParam = Expression.Parameter(current.Expression.Type, $"c1_{i}");
                var instanceParam = Expression.Parameter(current.Expression.Type, $"c_{i}");
                var withCall = Expression.Call(
                    genericWithMethod,
                    instanceParam, // Instance
                    Expression.Lambda( // propertyPicker
                        Expression.MakeMemberAccess(
                            memberAccessParam,
                            current.Member
                        ),
                        memberAccessParam
                    ),
                    propertyValue // propertyValueFactory
                );

                return Expression.Lambda(withCall, instanceParam);
            }

            Queue<MemberExpression> GetMembers()
            {
                var memberExpressions = new Queue<MemberExpression>();
                var expr = picker.Body;

                while (expr is MemberExpression memberExpr)
                {
                    memberExpressions.Enqueue(memberExpr);
                    expr = memberExpr.Expression;
                }

                return memberExpressions;
            }
        }

        private static void Validate<T>(string propertyName, T instance)
        {
            // Can we set the property via constructor?
            var hasConstructorParameter = HasConstructorParameter(propertyName, instance);

            if (hasConstructorParameter) return;

            // Can we set the property via property setter?
            var hasPropertySetter = HasPropertySetter(propertyName, instance);

            if (hasPropertySetter) return;

            // If we cannot do either, then there is no point in continuing.
            throw new InvalidOperationException(
                $"Error calling {nameof(With)} on type {typeof(T)}: Property '{propertyName.ToPropertyCase()}' cannot be set via constructor or property setter. You can fix this by making the property settable or adding it as a constructor parameter."
            );
        }

        private static bool HasPropertySetter<T>(string propertyName, T instance)
        {
            return TypeUtils.GetPropertyDictionary(instance).TryGetValue(propertyName, out var propertyInfo) && propertyInfo.CanWrite;
        }

        //return TypeUtils.GetPropertyDictionary<T>().TryGetValue(propertyName, out var propertyInfo) && propertyInfo.CanWrite;
        private static bool HasConstructorParameter<T>(string propertyName, T instance)
        {
            var constructorParameters = TypeUtils.GetSuitableConstructor(instance).GetParameters();

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