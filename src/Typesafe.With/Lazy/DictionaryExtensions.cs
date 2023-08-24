using System;
using System.Collections.Generic;
using System.Linq;

namespace Typesafe.With.Lazy
{
    internal static class DictionaryExtensions
    {
        public static Dictionary<TKey, TValue> AddOrUpdate<TKey, TValue>(
            this Dictionary<TKey, TValue> self,
            TKey key,
            TValue value
        )
        {
            if (self == null) throw new ArgumentNullException(nameof(self));
            if (key == null) throw new ArgumentNullException(nameof(key));
            
            if (self.ContainsKey(key))
            {
                self[key] = value;
            }
            else
            {
                self.Add(key, value);
            }

            return self;
        }

        internal static Dictionary<TKey, TNewValue> UpdateValues<TKey, TValue, TNewValue>(
            this Dictionary<TKey, TValue> self,
            Func<TValue, TNewValue> mapping
        )
        {
            return self
                .Select(pair => new KeyValuePair<TKey, TNewValue>(
                        key: pair.Key,
                        value: mapping(pair.Value)
                    )
                ).ToDictionary(pair => pair.Key, pair => pair.Value);
        }
    }
}