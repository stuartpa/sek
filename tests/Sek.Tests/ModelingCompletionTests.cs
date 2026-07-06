using System.Linq;
using Sek.Modeling;
using Xunit;

namespace Sek.Tests;

/// <summary>Completes <c>Sek.Modeling</c> coverage: remaining value-container operations
/// (Remove/Contains/enumerate/equality/hashing/ToString) and the parameterless
/// <see cref="RuleAttribute"/>.</summary>
public class ModelingCompletionTests
{
    [Fact]
    public void Set_Remove_Equality_Null_And_Hash()
    {
        var s = new Set<int>(1, 2, 3);
        var r = s.Remove(2);
        Assert.False(r.Contains(2));
        Assert.Equal(2, r.Count);
        Assert.False(s.Equals(null));
        Assert.Equal(new Set<int>(3, 2, 1).GetHashCode(), s.GetHashCode());
    }

    [Fact]
    public void Sequence_ParamsCtor_Contains_Remove_Enumerate_Equality_Hash_ToString()
    {
        var s = new Sequence<int>(1, 2, 2, 3);
        Assert.True(s.Contains(2));
        Assert.False(s.Contains(9));
        var r = s.Remove(2); // removes first occurrence
        Assert.Equal(new[] { 1, 2, 3 }, r.ToArray());
        Assert.Equal(new[] { 1, 2, 2, 3 }, s.ToArray()); // immutable
        Assert.False(s.Equals(null));
        Assert.Equal(new Sequence<int>(1, 2, 2, 3).GetHashCode(), s.GetHashCode());
        Assert.Equal("[1, 2, 2, 3]", s.ToString());
    }

    [Fact]
    public void Map_Equality_Null_And_Hash_And_Enumerate()
    {
        var m = new Map<string, int>().Add("a", 1).Add("b", 2);
        Assert.False(m.Equals(null));
        Assert.NotEqual(m, new Map<string, int>().Add("a", 1)); // different count
        Assert.NotEqual(m, new Map<string, int>().Add("a", 1).Add("b", 99)); // different value
        Assert.Equal(new[] { "a", "b" }, m.Select(kv => kv.Key).ToArray());
        Assert.Equal(new Map<string, int>().Add("b", 2).Add("a", 1).GetHashCode(), m.GetHashCode());
    }

    [Fact]
    public void RuleAttribute_Parameterless_HasNullAction()
    {
        Assert.Null(new RuleAttribute().Action);
        Assert.Equal("X.Y", new RuleAttribute("X.Y").Action);
    }
}
