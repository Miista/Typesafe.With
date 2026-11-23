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
            public IReadOnlyList<PropertyInfo> PropertyPath
            {
                get
                {
                    // Strip boxing/conversion, e.g. x => (object)x.Age
                    var body = expression.Body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary
                        ? unary.Operand
                        : expression.Body;

                    var members = new List<MemberExpression>();

                    while (body is MemberExpression member)
                    {
                        members.Insert(0, member);
                        body = member.Expression;
                    }

                    if (members.Count == 0)
                    {
                        throw new InvalidOperationException($"Cannot retrieve property from expression '{expression}'");
                    }

                    // The path must start at the lambda parameter, e.g. not at a static or captured object, or an indexer or method call
                    if (body != expression.Parameters[0])
                    {
                        throw new InvalidOperationException(
                            $"Expression '{expression}' must access properties starting from the parameter (static, captured, indexed or method call results are not supported)"
                        );
                    }

                    var path = new List<PropertyInfo>(members.Count);

                    foreach (var member in members)
                    {
                        if (member.Member is not PropertyInfo property)
                        {
                            throw new InvalidOperationException(
                                $"Expression '{expression}' refers to '{member.Member.Name}', which is not a property"
                            );
                        }

                        path.Add(property);
                    }

                    return path;
                }
            }
        }
    }
}
