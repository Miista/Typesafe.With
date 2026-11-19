using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Typesafe.With
{
    internal static class ExpressionExtensions
    {
        extension<T, TProperty>(Expression<Func<T, TProperty>> expression)
        {
            public string PropertyName
            {
                get
                {
                    // Strip boxing/conversion, e.g. x => (object)x.Age
                    var body = expression.Body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary
                        ? unary.Operand
                        : expression.Body;

                    return body switch
                    {
                        // The only valid shape: a property accessed directly on the lambda parameter
                        MemberExpression { Member: PropertyInfo property } member when member.Expression == expression.Parameters[0]
                            => property.Name.ToParameterCase(),

                        MemberExpression { Member: not PropertyInfo } member => throw new InvalidOperationException(
                            $"Expression '{expression}' refers to '{member.Member.Name}', which is not a property"
                        ),

                        MemberExpression => throw new InvalidOperationException(
                            $"Expression '{expression}' must access a property directly on the parameter (nested, static or captured properties are not supported)"
                        ),

                        _ => throw new InvalidOperationException($"Cannot retrieve property from expression '{expression}'")
                    };
                }
            }
        }
    }
}