using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Typesafe.With
{
    internal static class ExpressionExtensions
    {
        extension<T, TProperty>(Expression<Func<T, TProperty>> expression)
        {
            /// <summary>
            /// The chain of properties selected by the expression, from the parameter to the leaf,
            /// e.g. <c>[Owner, Address, City]</c> for <c>c =&gt; c.Owner.Address.City</c>.
            /// </summary>
            public IReadOnlyList<PropertyInfo> PropertyPath => GetPropertyPath(expression.Body, expression.Parameters[0], expression);
        }

        private static List<PropertyInfo> GetPropertyPath(Expression? node, ParameterExpression parameter, LambdaExpression expression) =>
            node switch
            {
                // A converted property, e.g. p => (object) p.Age; only the outermost conversion is stripped
                UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } convert when convert == expression.Body
                    => GetPropertyPath(convert.Operand, parameter, expression),

                // A property accessed directly on the parameter, e.g. p => p.Name
                MemberExpression { Member: PropertyInfo property } member when member.Expression == parameter
                    => [property],

                // A property accessed on another property, e.g. p => p.Address.City
                MemberExpression { Member: PropertyInfo property, Expression: MemberExpression parent }
                    => [..GetPropertyPath(parent, parameter, expression), property],

                // A field on the parameter or on a property, e.g. p => p.Field
                MemberExpression { Member: not PropertyInfo, Expression: ParameterExpression or MemberExpression } member => throw new InvalidOperationException(
                    $"Expression '{expression}' refers to '{member.Member.Name}', which is not a property"
                ),

                // Anything not rooted at the parameter, e.g. _ => captured.Name, _ => Static.Name or p => p.Items[0].Name
                MemberExpression => throw new InvalidOperationException(
                    $"Expression '{expression}' must access properties starting from the parameter (static, captured, indexed or method call results are not supported)"
                ),

                _ => throw new InvalidOperationException($"Cannot retrieve property from expression '{expression}'")
            };
    }
}
