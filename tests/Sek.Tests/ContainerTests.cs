using System.Text.Json;
using Sek.Engine;
using Sek.Modeling;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// Item 8 — value-typed containers (<see cref="Set{T}"/>, <see cref="Sequence{T}"/>,
/// <see cref="Map{K,V}"/>) as model <em>state fields</em>. Verifies JSON round-trip via the
/// container converters and order-independent (structural) hashing during exploration.
/// </summary>
public class ContainerTests
{
    private static JsonSerializerOptions Options() => new()
    {
        Converters = { new ContainerJsonConverterFactory() },
    };

    [Fact]
    public void Set_RoundTrips()
    {
        var s = new Set<int>(3, 1, 2);
        var json = JsonSerializer.Serialize(s, Options());
        var back = JsonSerializer.Deserialize<Set<int>>(json, Options())!;
        Assert.Equal(s, back);       // value equality preserved
        Assert.Equal(3, back.Count); // contents preserved (not lost)
    }

    [Fact]
    public void Set_SerializesCanonically_OrderIndependent()
    {
        var a = new Set<int>(1, 2, 3);
        var b = new Set<int>(3, 2, 1);
        Assert.Equal(JsonSerializer.Serialize(a, Options()), JsonSerializer.Serialize(b, Options()));
    }

    [Fact]
    public void Sequence_RoundTrips_AndKeepsOrder()
    {
        var s = new Sequence<int>(1, 2, 1);
        var json = JsonSerializer.Serialize(s, Options());
        var back = JsonSerializer.Deserialize<Sequence<int>>(json, Options())!;
        Assert.Equal(s, back);
        Assert.Equal(new[] { 1, 2, 1 }, back.ToArray());
        // Order matters for a sequence.
        Assert.NotEqual(JsonSerializer.Serialize(new Sequence<int>(2, 1), Options()),
                        JsonSerializer.Serialize(new Sequence<int>(1, 2), Options()));
    }

    [Fact]
    public void Sequence_Remove_RemovesFirstOccurrence()
    {
        var s = new Sequence<int>(1, 2, 1).Remove(1);
        Assert.Equal(new[] { 2, 1 }, s.ToArray());
    }

    [Fact]
    public void Map_RoundTrips_AndIsKeyOrderIndependent()
    {
        var m = new Map<string, int>().Add("b", 2).Add("a", 1);
        var json = JsonSerializer.Serialize(m, Options());
        var back = JsonSerializer.Deserialize<Map<string, int>>(json, Options())!;
        Assert.Equal(m, back);
        Assert.Equal(1, back["a"]);
        Assert.Equal(2, back["b"]);

        var m2 = new Map<string, int>().Add("a", 1).Add("b", 2);
        Assert.Equal(JsonSerializer.Serialize(m2, Options()), json); // canonical by key
    }

    [Fact]
    public void Explorer_SetStateField_IsDeduplicatedStructurally()
    {
        // The model can add elements in either order; the two orders reach the SAME set state,
        // which must be deduplicated to a single graph state (structural equality via the hash).
        var introspector = new ModelIntrospector(typeof(SetModel));
        var result = new Explorer(introspector, new ExplorationOptions { MaxDepth = 50, MaxStates = 100 }).Explore("SetModel");

        // States: {}, {1}, {2}, {1,2}. Adding 1-then-2 and 2-then-1 both land on {1,2}.
        Assert.Equal(4, result.Graph.States.Count);
    }

    /// <summary>A model whose entire state is a <see cref="Set{T}"/> of ints (0..2 elements).</summary>
    public sealed class SetModel : ModelProgram
    {
        public Set<int> Items { get; set; } = new Set<int>();

        [Rule("Add1")]
        public void Add1() { Require(!Items.Contains(1), "has 1"); Items = Items.Add(1); }

        [Rule("Add2")]
        public void Add2() { Require(!Items.Contains(2), "has 2"); Items = Items.Add(2); }
    }
}
