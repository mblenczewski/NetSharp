using System.Collections.Concurrent;

namespace NetSharp.Utils
{
    /// <summary>
    /// Represents a concurrent two-way dictionary, that can be indexed by either a key or a value.
    /// </summary>
    /// <typeparam name="K">The type of key that will be stored.</typeparam>
    /// <typeparam name="V">The type of value that will be stored.</typeparam>
    public class BiDictionary<K, V>
    {
        /// <summary>
        /// Maps keys to their corresponding values.
        /// </summary>
        private readonly ConcurrentDictionary<K, V> keyToValueMap;

        /// <summary>
        /// Maps values to their corresponding keys.
        /// </summary>
        private readonly ConcurrentDictionary<V, K> valueToKeyMap;

        /// <summary>
        /// Initialises a new instance of the <see cref="BiDictionary{K,V}" /> class.
        /// </summary>
        public BiDictionary()
        {
            keyToValueMap = new ConcurrentDictionary<K, V>();

            valueToKeyMap = new ConcurrentDictionary<V, K>();
        }

        /// <summary>
        /// Indexes this instance with the given value.
        /// </summary>
        /// <param name="index">The value whose key to get or set.</param>
        /// <returns>The fetched key.</returns>
        public K this[V index]
        {
            get
            {
                valueToKeyMap.TryGetValue(index, out K key);

                return key;
            }

            set { valueToKeyMap.AddOrUpdate(index, value, (v, k) => value); }
        }

        /// <summary>
        /// Indexes this instance with the given key.
        /// </summary>
        /// <param name="index">The key whose value to get or set.</param>
        /// <returns>The fetched value.</returns>
        public V this[K index]
        {
            get
            {
                keyToValueMap.TryGetValue(index, out V value);

                return value;
            }

            set { keyToValueMap.AddOrUpdate(index, value, (k, v) => value); }
        }

        /// <summary>
        /// Clears this instance's <see cref="keyToValueMap" /> and <see cref="valueToKeyMap" />.
        /// </summary>
        public void Clear()
        {
            keyToValueMap.Clear();
            valueToKeyMap.Clear();
        }

        /// <summary>
        /// Whether this instance contains the given key.
        /// </summary>
        /// <param name="key">The key to check.</param>
        /// <returns>Whether the given key was found.</returns>
        public bool ContainsKey(in K key) => keyToValueMap.ContainsKey(key);

        /// <summary>
        /// Whether this instance contains the given value.
        /// </summary>
        /// <param name="value">The value to check.</param>
        /// <returns>Whether the given value was found.</returns>
        public bool ContainsValue(in V value) => valueToKeyMap.ContainsKey(value);

        /// <summary>
        /// Attempts to set the key associated with the given value.
        /// </summary>
        /// <param name="value">The value whose key to set.</param>
        /// <param name="key">The new value for the value's associated key.</param>
        /// <returns>Whether the new key was correctly set.</returns>
        public void SetOrUpdateKey(V value, K key)
        {
            valueToKeyMap.AddOrUpdate(value, key, (v, k) => key);

            keyToValueMap.AddOrUpdate(key, value, (k, v) => value);
        }

        /// <summary>
        /// Attempts to set the value associated with the given key.
        /// </summary>
        /// <param name="key">The key whose value to set.</param>
        /// <param name="value">The new value for the key's associated value.</param>
        /// <returns>Whether the new value was correctly set.</returns>
        public void SetOrUpdateValue(K key, V value)
        {
            keyToValueMap.AddOrUpdate(key, value, (k, v) => value);

            valueToKeyMap.AddOrUpdate(value, key, (v, k) => key);
        }

        /// <summary>
        /// Attempts to remove the key associated with the given value.
        /// </summary>
        /// <param name="value">The value whose key to remove.</param>
        /// <param name="key">The old key value.</param>
        /// <returns>Whether the given value had a valid key associated with it.</returns>
        public bool TryClearKey(in V value, out K key)
        {
            bool clearedValue = valueToKeyMap.TryRemove(value, out key);

            bool clearedKey = keyToValueMap.TryRemove(key, out _);

            return clearedValue && clearedKey;
        }

        /// <summary>
        /// Attempts to remove the value associated with the given key.
        /// </summary>
        /// <param name="key">The key whose value to remove.</param>
        /// <param name="value">The old value.</param>
        /// <returns>Whether the given key had a valid valid associated with it.</returns>
        public bool TryClearValue(in K key, out V value)
        {
            bool clearedKey = keyToValueMap.TryRemove(key, out value);

            bool clearedValue = valueToKeyMap.TryRemove(value, out _);

            return clearedKey && clearedValue;
        }

        /// <summary>
        /// Attempts to get the key associated with the given value.
        /// </summary>
        /// <param name="value">The value whose key to get.</param>
        /// <param name="key">The returned key.</param>
        /// <returns>Whether the given value has a valid key associated with it.</returns>
        public bool TryGetKey(in V value, out K key)
        {
            return valueToKeyMap.TryGetValue(value, out key);
        }

        /// <summary>
        /// Attempts to get the value associated with the given key.
        /// </summary>
        /// <param name="key">The key whose value to get.</param>
        /// <param name="value">The returned value.</param>
        /// <returns>Whether the given key as a valid value associated with it.</returns>
        public bool TryGetValue(in K key, out V value)
        {
            return keyToValueMap.TryGetValue(key, out value);
        }

        /// <summary>
        /// Attempts to set the key associated with the given value.
        /// </summary>
        /// <param name="value">The value whose key to set.</param>
        /// <param name="key">The key which should be set for the given value.</param>
        /// <returns>Whether the given value was successfully set.</returns>
        public bool TrySetKey(in V value, in K key)
        {
            K newKey = key;
            V newValue = value;

            K setKey = valueToKeyMap.AddOrUpdate(value, v => newKey, (v, k) => newKey);
            V setValue = keyToValueMap.AddOrUpdate(key, k => newValue, (k, v) => newValue);

            return (setKey?.Equals(key) ?? false) && (setValue?.Equals(value) ?? false);
        }

        /// <summary>
        /// Attempts to set the value associated with the given key.
        /// </summary>
        /// <param name="key">The key whose value to set.</param>
        /// <param name="value">The value which should be set for the given key.</param>
        /// <returns>Whether the given key was successfully set.</returns>
        public bool TrySetValue(in K key, in V value)
        {
            V newValue = value;
            K newKey = key;

            V setValue = keyToValueMap.AddOrUpdate(key, k => newValue, (k, v) => newValue);
            K setKey = valueToKeyMap.AddOrUpdate(value, v => newKey, (v, k) => newKey);

            return (setValue?.Equals(value) ?? false) && (setKey?.Equals(key) ?? false);
        }
    }
}