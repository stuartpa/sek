using System.Linq;
using Sek.Modeling;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// Coverage for <c>Sek.Modeling</c>: the value-typed containers (<see cref="Set{T}"/>,
/// <see cref="Sequence{T}"/>, <see cref="Map{K,V}"/>), the guard helpers
/// (<see cref="Condition"/>, <see cref="ModelProgram"/>), and requirement capture.
/// </summary>
public class ModelingTests
{
    // ---- Set<T> ------------------------------------------------------------------------

    [Fact]
    public void Set_IsUnordered_ValueEqual_AndImmutable()
    {
        var a = new Set<int>(1, 2, 3);
        var b = new Set<int>(3, 2, 1);
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());

        var c = a.Add(4);
        Assert.Equal(3, a.Count);       // original unchanged (immutable)
        Assert.Equal(4, c.Count);
        Assert.True(c.Contains(4));

        var d = c.Remove(4);
        Assert.Equal(a, d);
        Assert.Equal("{1, 2, 3}", a.ToString());
    }

    [Fact]
    public void Set_EnumeratesInDeterministicOrder()
    {
        var s = new Set<string>("banana", "apple", "cherry");
        Assert.Equal(new[] { "apple", "banana", "cherry" }, s.ToArray());
    }

    // ---- Sequence<T> -------------------------------------------------------------------

    [Fact]
    public void Sequence_IsOrdered_ValueEqual_AndImmutable()
    {
        var a = new Sequence<int>(1, 2, 3);
        var b = new Sequence<int>(1, 2, 3);
        Assert.Equal(a, b);
        Assert.NotEqual(a, new Sequence<int>(3, 2, 1)); // order matters
        Assert.Equal(3, a[2]);
        Assert.True(a.Contains(2));

        var c = a.Add(4);
        Assert.Equal(3, a.Count);
        Assert.Equal(4, c.Count);
        Assert.Equal(a, c.Remove(4));
        Assert.Equal("[1, 2, 3]", a.ToString());
    }

    // ---- Map<K,V> ----------------------------------------------------------------------

    [Fact]
    public void Map_IsValueEqual_AndImmutable()
    {
        var a = new Map<string, int>().Add("x", 1).Add("y", 2);
        var b = new Map<string, int>().Add("y", 2).Add("x", 1);
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.True(a.ContainsKey("x"));
        Assert.Equal(1, a["x"]);
        Assert.Equal(2, a.Count);

        var c = a.Remove("x");
        Assert.False(c.ContainsKey("x"));
        Assert.NotEqual(a, c);
        Assert.Equal(2, a.Count); // original unchanged
    }

    [Fact]
    public void Map_Enumerates_SortedByKey()
    {
        var m = new Map<string, int>().Add("b", 2).Add("a", 1);
        Assert.Equal(new[] { "a", "b" }, m.Select(kv => kv.Key).ToArray());
    }

    // ---- Guards / conditions -----------------------------------------------------------

    [Fact]
    public void Condition_IsTrue_ThrowsWhenFalse()
    {
        Condition.IsTrue(true, "ok"); // no throw
        var ex = Assert.Throws<GuardDisabledException>(() => Condition.IsTrue(false, "nope"));
        Assert.Contains("nope", ex.Message);
    }

    [Fact]
    public void ModelProgram_Require_DisablesActionWhenFalse()
    {
        var model = new TinyModel();
        model.Enable(); // sets a flag so the guarded rule is enabled
        model.Guarded(); // does not throw when enabled

        var blocked = new TinyModel();
        Assert.Throws<GuardDisabledException>(() => blocked.Guarded());
    }

    private sealed class TinyModel : ModelProgram
    {
        public bool Ready { get; set; }
        public void Enable() => Ready = true;
        public void Guarded() => Require(Ready, "not ready");
    }

    // ---- Requirement capture -----------------------------------------------------------

    [Fact]
    public void Requirement_Capture_Reset_Captured()
    {
        Requirement.Reset();
        Assert.Empty(Requirement.Captured);
        Requirement.Capture("REQ-1");
        Requirement.Capture("");        // ignored (blank)
        Requirement.Capture("REQ-2");
        Assert.Equal(new[] { "REQ-1", "REQ-2" }, Requirement.Captured);
        Requirement.Reset();
        Assert.Empty(Requirement.Captured);
    }
}
