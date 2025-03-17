using System;
using System.Linq.Expressions;

namespace Typesafe.With
{
    internal class RecordWithBuilder<T>
    {
        public T Construct<TProperty>(T instance, Expression<Func<T, TProperty>> picker, Expression<Func<TProperty, TProperty>> valueFactory)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            
            var currentValue = picker.Compile().Invoke(instance);
            var newValue = valueFactory.Compile().Invoke(currentValue);
            
            return Construct(instance, picker, newValue);
        }
        
        public T Construct<TProperty>(T instance, Expression<Func<T, TProperty>> picker, TProperty newValue)
        {
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            
            T newInstance;
            var propertyInfo = picker.GetProperty() ?? throw new Exception();
            var propertyInfoSetMethod = propertyInfo.SetMethod ?? throw new Exception();

            if (typeof(T).IsValueType)
            {
                newInstance = instance;
                propertyInfoSetMethod.Invoke(newInstance, [newValue]);
            }
            else
            {
                var methodInfo = typeof(T).GetMethod("<Clone>$") ?? throw new Exception();
                    
                newInstance = (T) methodInfo.Invoke(instance, []);
                propertyInfoSetMethod.Invoke(newInstance, [newValue]);
                    
            }
                
            return newInstance;
        }
    }
}