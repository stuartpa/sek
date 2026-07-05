using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sek.Modeling
{
    /// <summary>
    /// System.Text.Json converters for the value-typed containers so they round-trip as model
    /// <em>state fields</em>. The immutable containers expose an <c>Add</c> that returns a new
    /// instance (not the mutable-collection pattern STJ expects), so without these converters a
    /// deserialized state would lose its container contents. Sets and maps are written in a
    /// deterministic (sorted) order so the canonical state hash is order-independent; sequences
    /// preserve element order.
    /// </summary>
    public sealed class ContainerJsonConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type t)
        {
            if (!t.IsGenericType) return false;
            var def = t.GetGenericTypeDefinition();
            return def == typeof(Set<>) || def == typeof(Sequence<>) || def == typeof(Map<,>);
        }

        public override JsonConverter CreateConverter(Type t, JsonSerializerOptions options)
        {
            var def = t.GetGenericTypeDefinition();
            var args = t.GetGenericArguments();
            var converterType = def == typeof(Set<>) ? typeof(SetConverter<>).MakeGenericType(args)
                : def == typeof(Sequence<>) ? typeof(SequenceConverter<>).MakeGenericType(args)
                : typeof(MapConverter<,>).MakeGenericType(args);
            return (JsonConverter)Activator.CreateInstance(converterType)!;
        }

        private sealed class SetConverter<T> : JsonConverter<Set<T>>
        {
            public override Set<T> Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions o)
            {
                var items = JsonSerializer.Deserialize<List<T>>(ref reader, o) ?? new List<T>();
                return new Set<T>(items);
            }

            public override void Write(Utf8JsonWriter writer, Set<T> value, JsonSerializerOptions o)
            {
                // Set<T> enumerates in a deterministic (sorted-by-string) order already.
                JsonSerializer.Serialize(writer, value.ToList(), o);
            }
        }

        private sealed class SequenceConverter<T> : JsonConverter<Sequence<T>>
        {
            public override Sequence<T> Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions o)
            {
                var items = JsonSerializer.Deserialize<List<T>>(ref reader, o) ?? new List<T>();
                return new Sequence<T>(items);
            }

            public override void Write(Utf8JsonWriter writer, Sequence<T> value, JsonSerializerOptions o) =>
                JsonSerializer.Serialize(writer, value.ToList(), o);
        }

        private sealed class MapConverter<K, V> : JsonConverter<Map<K, V>> where K : notnull
        {
            public override Map<K, V> Read(ref Utf8JsonReader reader, Type t, JsonSerializerOptions o)
            {
                var pairs = JsonSerializer.Deserialize<List<Entry>>(ref reader, o) ?? new List<Entry>();
                return new Map<K, V>(pairs.Select(p => new KeyValuePair<K, V>(p.Key, p.Value)));
            }

            public override void Write(Utf8JsonWriter writer, Map<K, V> value, JsonSerializerOptions o)
            {
                // Map<K,V> enumerates key/value pairs in a deterministic (sorted-by-key) order.
                var entries = value.Select(kv => new Entry { Key = kv.Key, Value = kv.Value }).ToList();
                JsonSerializer.Serialize(writer, entries, o);
            }

            private sealed class Entry
            {
                public K Key { get; set; } = default!;
                public V Value { get; set; } = default!;
            }
        }
    }
}
