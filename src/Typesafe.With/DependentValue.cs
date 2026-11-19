using System;
using System.Linq.Expressions;
using System.Reflection;

namespace Typesafe.With
{
    internal readonly struct DependentValue(LambdaExpression expression)
    {
        private readonly LambdaExpression expression = expression ?? throw new ArgumentNullException(nameof(expression));

        public object Resolve(PropertyInfo existingProperty, object instance)
        {
            var compiledExpression = expression.Compile();
            var existingValue = existingProperty.GetValue(instance);
                
            return compiledExpression.DynamicInvoke(existingValue);
        }
    }
}