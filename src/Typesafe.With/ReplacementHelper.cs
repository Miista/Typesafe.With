using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;

namespace Typesafe.With
{
    internal static class ReplacementHelper
    {
        public static Replacement Create<T, TProperty>(Expression<Func<T, TProperty>> propertyPicker, TProperty value)
        {
            return Create(propertyPicker, (object)value);
        }
        
        public static Replacement Create<T, TProperty>(Expression<Func<T, TProperty>> propertyPicker, Func<TProperty, TProperty> valueFactory)
        {
            return Create(propertyPicker, (object)valueFactory);
        }

        private static Replacement Create<T, TProperty>(Expression<Func<T, TProperty>> propertyPicker, object valueOrValueFactory)
        {
            var body = propertyPicker.Body;

            if (body is UnaryExpression { Operand: MemberExpression { Member: PropertyInfo prop, NodeType: ExpressionType.MemberAccess } })
            {
                return new Replacement(prop, valueOrValueFactory, null);
            }
            
            if (body is not MemberExpression { Member: PropertyInfo property } expression)
                throw new InvalidOperationException("Expression must be a property expression");

            // Nested property?
            if (expression.NodeType == ExpressionType.MemberAccess)
            {
                var stack = new Stack<MemberInfo>();
                var memberExpression = expression.Expression as MemberExpression;
                while (memberExpression?.NodeType == ExpressionType.MemberAccess)
                {
                    stack.Push(memberExpression.Member);
                    memberExpression = memberExpression.Expression as MemberExpression;
                }
                
                return new Replacement(property, valueOrValueFactory, stack);
            }
            
            return new Replacement(property, valueOrValueFactory, null);
        }
    }
}