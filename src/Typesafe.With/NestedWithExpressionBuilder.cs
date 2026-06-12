using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Typesafe.With
{
    internal static class NestedWithExpressionBuilder
    {
        private static readonly MethodInfo WithMethod = typeof(ObjectExtensions)
            .GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
            .SingleOrDefault(m =>
                m.Name == "InternalWith"
                && m.GetParameters()[1].ParameterType.GetGenericTypeDefinition() == typeof(Expression<>)
                && m.GetParameters()[2].ParameterType.GetGenericTypeDefinition() == typeof(Expression<>)
            ) ?? throw new InvalidOperationException("Unable to find suitable method for InternalWith");

        public static Expression<Func<T, T>> Build<T, TValue>(
            Expression<Func<T, TValue>> propertyPicker,
            Expression<Func<TValue, TValue>> propertyValueFactory
        )
        {
            var memberChain = GetMemberChainFromLeaf(propertyPicker);

            // Build leaf
            var nestedWithExpression = BuildLambda(
                memberChain[0],
                depth: 0,
                propertyValue: propertyValueFactory
            );

            // Build chain from leaf to root
            for (var depth = 1; depth < memberChain.Count; depth++)
            {
                nestedWithExpression = BuildLambda(
                    memberChain[depth],
                    depth,
                    nestedWithExpression
                );
            }

            return (Expression<Func<T, T>>)nestedWithExpression;
        }

        private static IReadOnlyList<MemberExpression> GetMemberChainFromLeaf<T, TValue>(
            Expression<Func<T, TValue>> propertyPicker
        )
        {
            var members = new List<MemberExpression>();
            var expression = propertyPicker.Body;

            while (expression is MemberExpression memberExpression)
            {
                members.Add(memberExpression);
                expression = memberExpression.Expression;
            }

            return members;
        }

        private static LambdaExpression BuildLambda(MemberExpression current, int depth, Expression propertyValue)
        {
            var genericWithMethod = WithMethod.MakeGenericMethod(current.Expression.Type, current.Type);
            var memberAccessParam = Expression.Parameter(current.Expression.Type, $"c1_{depth}");
            var instanceParam = Expression.Parameter(current.Expression.Type, $"c_{depth}");
            var withCall = Expression.Call(
                genericWithMethod,
                instanceParam,
                Expression.Lambda(
                    Expression.MakeMemberAccess(memberAccessParam, current.Member),
                    memberAccessParam
                ),
                propertyValue
            );

            return Expression.Lambda(withCall, instanceParam);
        }
    }
}
