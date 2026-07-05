using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Sek.Modeling
{
    /// <summary>
    /// An unordered value-typed set (mirrors <c>Microsoft.Modeling.Set&lt;T&gt;</c>). Two sets are
    /// equal when they contain the same elements regardless of order. Immutable: mutators return
    /// a new set. Enumerates in a deterministic (sorted-by-string) order so serialized state is
    /// stable.
    /// </summary>
    public sealed class Set<T> : IEnumerable<T>, IEquatable<Set<T>>
    {
        private readonly HashSet<T> _items;

        public Set() => _items = new HashSet<T>();

        public Set(IEnumerable<T> items) => _items = new HashSet<T>(items);

        public Set(params T[] items) => _items = new HashSet<T>(items);

        public int Count => _items.Count;

        public bool Contains(T item) => _items.Contains(item);

        public Set<T> Add(T item) { var s = new HashSet<T>(_items) { item }; return new Set<T>(s); }

        public Set<T> Remove(T item) { var s = new HashSet<T>(_items); s.Remove(item); return new Set<T>(s); }

        public IEnumerator<T> GetEnumerator() =>
            _items.OrderBy(x => x?.ToString(), StringComparer.Ordinal).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool Equals(Set<T>? other) => other is not null && _items.SetEquals(other._items);

        public override bool Equals(object? obj) => Equals(obj as Set<T>);

        public override int GetHashCode()
        {
            var h = 0;
            foreach (var i in _items) h ^= i?.GetHashCode() ?? 0; // order-independent
            return h;
        }

        public override string ToString() => "{" + string.Join(", ", this) + "}";
    }

    /// <summary>
    /// An ordered value-typed sequence (mirrors <c>Microsoft.Modeling.Sequence&lt;T&gt;</c>). Two
    /// sequences are equal when they have the same elements in the same order. Immutable.
    /// </summary>
    public sealed class Sequence<T> : IReadOnlyList<T>, IEquatable<Sequence<T>>
    {
        private readonly List<T> _items;

        public Sequence() => _items = new List<T>();

        public Sequence(IEnumerable<T> items) => _items = new List<T>(items);

        public Sequence(params T[] items) => _items = new List<T>(items);

        public int Count => _items.Count;

        public T this[int index] => _items[index];

        public Sequence<T> Add(T item) { var l = new List<T>(_items) { item }; return new Sequence<T>(l); }

        public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool Equals(Sequence<T>? other) => other is not null && _items.SequenceEqual(other._items);

        public override bool Equals(object? obj) => Equals(obj as Sequence<T>);

        public override int GetHashCode()
        {
            var h = 17;
            foreach (var i in _items) h = (h * 31) + (i?.GetHashCode() ?? 0);
            return h;
        }

        public override string ToString() => "[" + string.Join(", ", _items) + "]";
    }

    /// <summary>
    /// A value-typed map (mirrors <c>Microsoft.Modeling.Map&lt;K,V&gt;</c>). Two maps are equal
    /// when they hold the same key/value pairs. Immutable.
    /// </summary>
    public sealed class Map<K, V> : IEnumerable<KeyValuePair<K, V>>, IEquatable<Map<K, V>>
        where K : notnull
    {
        private readonly Dictionary<K, V> _items;

        public Map() => _items = new Dictionary<K, V>();

        public Map(IEnumerable<KeyValuePair<K, V>> items) => _items = new Dictionary<K, V>(items);

        public int Count => _items.Count;

        public bool ContainsKey(K key) => _items.ContainsKey(key);

        public V this[K key] => _items[key];

        public Map<K, V> Add(K key, V value) { var d = new Dictionary<K, V>(_items) { [key] = value }; return new Map<K, V>(d); }

        public Map<K, V> Remove(K key) { var d = new Dictionary<K, V>(_items); d.Remove(key); return new Map<K, V>(d); }

        public IEnumerator<KeyValuePair<K, V>> GetEnumerator() =>
            _items.OrderBy(kv => kv.Key.ToString(), StringComparer.Ordinal).GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public bool Equals(Map<K, V>? other)
        {
            if (other is null || _items.Count != other._items.Count) return false;
            foreach (var kv in _items)
            {
                if (!other._items.TryGetValue(kv.Key, out var v) || !EqualityComparer<V>.Default.Equals(kv.Value, v)) return false;
            }

            return true;
        }

        public override bool Equals(object? obj) => Equals(obj as Map<K, V>);

        public override int GetHashCode()
        {
            var h = 0;
            foreach (var kv in _items) h ^= HashCode.Combine(kv.Key, kv.Value); // order-independent
            return h;
        }
    }
}
