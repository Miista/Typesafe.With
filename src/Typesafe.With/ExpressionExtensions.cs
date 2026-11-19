using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Typesafe.With
{
    internal static class ExpressionExtensions
    {
        extension<T, TProperty>(Expression<Func<T, TProperty>> expression)
        {
            public string GetPropertyName()
            {
                if (expression == null) throw new ArgumentNullException(nameof(expression));

                return expression.Body switch
                {
                    UnaryExpression { Operand: MemberExpression um } => (um.Member as PropertyInfo)?.Name.ToParameterCase()!,
                    MemberExpression { Expression.NodeType: ExpressionType.MemberAccess } => throw new InvalidOperationException(
                        $"Expression '{expression}' represents a nested property"
                    ),
                    MemberExpression m => (m.Member as PropertyInfo)?.Name.ToParameterCase()!,
                    _ => throw new InvalidOperationException($"Cannot retrieve property from expression '{expression}'")
                };
            }
        }
    }
}