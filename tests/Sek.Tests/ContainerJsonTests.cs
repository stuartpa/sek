using System.Linq;
using System.Text.Json;
using Sek.Modeling;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// Coverage for <see cref="ContainerJsonConverterFactory"/> — the System.Text.Json converters that
/// let the value-typed containers round-trip as model state fields (order-deterministic).
/// </summary>
public class ContainerJsonTests
{
    private static readonly JsonSerializerOptions Opts = new() { Converters = { new ContainerJsonConverterFactory() } };

    [Fact]
    public void CanConvert_OnlyContainers()
    {
        var f = new ContainerJsonConverterFactory();
        Assert.True(f.CanConvert(typeof(Set<int>)));
        Assert.True(f.CanConvert(typeof(Sequence<string>)));
        Assert.True(f.CanConvert(typeof(Map<string, int>)));
        Assert.False(f.CanConvert(typeof(int)));           // non-generic
        Assert.False(f.CanConvert(typeof(System.Collections.Generic.List<int>))); // other generic
    }

    [Fact]
    public void Set_RoundTrips_OrderIndependent()
    {
        var s = new Set<int>(3, 1, 2);
        var json = JsonSerializer.Serialize(s, Opts);
        var back = JsonSerializer.Deserialize<Set<int>>(json, Opts);
        Assert.Equal(s, back);
    }

    [Fact]
    public void Sequence_RoundTrips_PreservesOrder()
    {
        var q = new Sequence<int>(1, 2, 2, 3);
        var json = JsonSerializer.Serialize(q, Opts);
        var back = JsonSerializer.Deserialize<Sequence<int>>(json, Opts);
        Assert.Equal(q, back);
        Assert.Equal(new[] { 1, 2, 2, 3 }, back!.ToArray());
    }

    [Fact]
    public void Map_RoundTrips()
    {
        var m = new Map<string, int>().Add("b", 2).Add("a", 1);
        var json = JsonSerializer.Serialize(m, Opts);
        var back = JsonSerializer.Deserialize<Map<string, int>>(json, Opts);
        Assert.Equal(m, back);
    }

    [Fact]
    public void Containers_WithNullElements_HashAndEqualityHandleNulls()
    {
        // exercise the `?? 0` / null-element branches in GetHashCode/Equals
        var a = new Set<string?>(new string?[] { "x", null });
        var b = new Set<string?>(new string?[] { null, "x" });
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());

        var s1 = new Sequence<string?>(new string?[] { null, "y" });
        Assert.Equal(s1.GetHashCode(), new Sequence<string?>(new string?[] { null, "y" }).GetHashCode());
    }
}
