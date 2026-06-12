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
            var members = GetMembers(propertyPicker);
            var root = members.Dequeue();
            var nestedWithExpression = BuildLambda(root, 0, propertyValueFactory);

            var depth = 1;
            while (members.Count > 0)
            {
                var member = members.Dequeue();
                nestedWithExpression = BuildLambda(member, depth, nestedWithExpression);
                depth++;
            }

            return nestedWithExpression as Expression<Func<T, T>>;
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

        private static Queue<MemberExpression> GetMembers<T, TValue>(Expression<Func<T, TValue>> propertyPicker)
        {
            var memberExpressions = new Queue<MemberExpression>();
            var expr = propertyPicker.Body;

            while (expr is MemberExpression memberExpr)
            {
                memberExpressions.Enqueue(memberExpr);
                expr = memberExpr.Expression;
            }

            return memberExpressions;
        }
    }
}
