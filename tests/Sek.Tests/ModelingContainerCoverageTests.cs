using System.Collections;
using System.Linq;
using System.Text.Json;
using Sek.Modeling;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// Branch coverage for the immutable value-typed containers (<see cref="Set{T}"/>,
/// <see cref="Sequence{T}"/>, <see cref="Map{K,V}"/>): the equality edge cases (null, wrong type,
/// count mismatch, missing key), hash codes, removal, membership, the non-generic enumerators, and
/// JSON round-tripping of empty containers.
/// </summary>
public class ModelingContainerCoverageTests
{
    [Fact]
    public void Set_Operations_And_Equality()
    {
        var s = new Set<int>(1, 2, 3);
        Assert.Equal(3, s.Count);
        Assert.True(s.Contains(2));
        Assert.False(s.Add(4).Equals(s));
        Assert.Equal(2, s.Remove(3).Count);
        Assert.True(s.Equals(new Set<int>(3, 2, 1)));           // order-independent
        Assert.False(s.Equals((Set<int>?)null));                // null → false
        Assert.False(s.Equals((object?)"not a set"));           // wrong type → false
        Assert.True(s.Equals((object)new Set<int>(1, 2, 3)));   // object overload
        Assert.Equal(new Set<int>(1, 2, 3).GetHashCode(), s.GetHashCode());
        Assert.Contains("1", s.ToString());
        Assert.Equal(3, ((IEnumerable)s).Cast<int>().Count());  // non-generic enumerator
    }

    [Fact]
    public void Sequence_Operations_And_Equality()
    {
        var q = new Sequence<int>(1, 2, 3);
        Assert.Equal(3, q.Count);
        Assert.Equal(2, q[1]);
        Assert.True(q.Contains(3));
        Assert.Equal(4, q.Add(4)[3]);
        Assert.Equal(2, q.Remove(2).Count);
        Assert.True(q.Equals(new Sequence<int>(1, 2, 3)));
        Assert.False(q.Equals(new Sequence<int>(3, 2, 1)));     // order matters
        Assert.False(q.Equals((Sequence<int>?)null));
        Assert.False(q.Equals((object?)42));
        Assert.True(q.Equals((object)new Sequence<int>(1, 2, 3)));
        Assert.Equal(new Sequence<int>(1, 2, 3).GetHashCode(), q.GetHashCode());
        Assert.Contains("2", q.ToString());
        Assert.Equal(3, ((IEnumerable)q).Cast<int>().Count());
    }

    [Fact]
    public void Map_Operations_And_Equality()
    {
        var m = new Map<string, int>().Add("a", 1).Add("b", 2);
        Assert.Equal(2, m.Count);
        Assert.True(m.ContainsKey("a"));
        Assert.Equal(2, m["b"]);
        Assert.Equal(1, m.Remove("b").Count);

        Assert.True(m.Equals(new Map<string, int>().Add("b", 2).Add("a", 1)));   // order-independent
        Assert.False(m.Equals((Map<string, int>?)null));                        // null → false
        Assert.False(m.Equals(new Map<string, int>().Add("a", 1)));             // count mismatch
        Assert.False(m.Equals(new Map<string, int>().Add("a", 1).Add("x", 9))); // missing key
        Assert.False(m.Equals(new Map<string, int>().Add("a", 1).Add("b", 99)));// value mismatch
        Assert.False(m.Equals((object?)"nope"));
        Assert.True(m.Equals((object)new Map<string, int>().Add("a", 1).Add("b", 2)));
        Assert.Equal(new Map<string, int>().Add("a", 1).Add("b", 2).GetHashCode(), m.GetHashCode());
        Assert.Equal(2, ((IEnumerable)m).Cast<object>().Count());
    }

    [Fact]
    public void Containers_NullElements_HashAndEquality()
    {
        // GetHashCode's `i?.GetHashCode() ?? 0` null-element branch for each container.
        var s = new Set<string?>("a", null);
        _ = s.GetHashCode();
        Assert.True(s.Contains(null));
        _ = s.ToString();          // enumerates → OrderBy(x?.ToString()) null-element branch

        var q = new Sequence<string?>("a", null);
        _ = q.GetHashCode();
        Assert.True(q.Contains(null));
        _ = q.ToString();

        // Map with a null value → equality's value comparison handles null.
        var m1 = new Map<string, string?>().Add("k", null);
        var m2 = new Map<string, string?>().Add("k", null);
        Assert.True(m1.Equals(m2));
        Assert.False(m1.Equals(new Map<string, string?>().Add("k", "v")));
        _ = m1.GetHashCode();
    }

    [Fact]
    public void Containers_JsonRoundTrip()
    {
        var opts = new JsonSerializerOptions { Converters = { new ContainerJsonConverterFactory() } };

        var set = new Set<int>(3, 1, 2);
        Assert.True(set.Equals(JsonSerializer.Deserialize<Set<int>>(JsonSerializer.Serialize(set, opts), opts)));

        var seq = new Sequence<string>("x", "y");
        Assert.True(seq.Equals(JsonSerializer.Deserialize<Sequence<string>>(JsonSerializer.Serialize(seq, opts), opts)));

        var map = new Map<string, int>().Add("k", 7);
        Assert.True(map.Equals(JsonSerializer.Deserialize<Map<string, int>>(JsonSerializer.Serialize(map, opts), opts)));

        // empty containers round-trip too
        Assert.Equal(0, JsonSerializer.Deserialize<Set<int>>(JsonSerializer.Serialize(new Set<int>(), opts), opts)!.Count);

        // the factory only converts the container types
        var factory = new ContainerJsonConverterFactory();
        Assert.True(factory.CanConvert(typeof(Set<int>)));
        Assert.True(factory.CanConvert(typeof(Map<string, int>)));
        Assert.False(factory.CanConvert(typeof(int)));
    }
}
