using SpecExplorerKit.Components.Json;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// Tests for the SpecExplorerKit.Components.Json component (canonical JSON + hashing). A component
/// carries no domain knowledge, so these tests exercise it purely as a generic building block.
/// </summary>
public class CanonicalJsonTests
{
    [Fact]
    public void Canonicalize_SortsObjectKeys()
    {
        Assert.Equal(
            CanonicalJson.Canonicalize("{\"b\":1,\"a\":2}"),
            CanonicalJson.Canonicalize("{\"a\":2,\"b\":1}"));
    }

    [Fact]
    public void Canonicalize_SortsArrayElements_SetSemantics()
    {
        // Arrays are order-independent under canonicalization (set-style equality).
        Assert.Equal(
            CanonicalJson.Canonicalize("[3,1,2]"),
            CanonicalJson.Canonicalize("[2,3,1]"));
    }

    [Fact]
    public void Canonicalize_IsRecursive()
    {
        var a = CanonicalJson.Canonicalize("{\"x\":{\"q\":1,\"p\":2},\"y\":[2,1]}");
        var b = CanonicalJson.Canonicalize("{\"y\":[1,2],\"x\":{\"p\":2,\"q\":1}}");
        Assert.Equal(a, b);
    }

    [Fact]
    public void Hash_IsStable_AndDiffersForDifferentContent()
    {
        var h1 = CanonicalJson.Hash(CanonicalJson.Canonicalize("{\"a\":1}"));
        var h2 = CanonicalJson.Hash(CanonicalJson.Canonicalize("{\"a\":1}"));
        var h3 = CanonicalJson.Hash(CanonicalJson.Canonicalize("{\"a\":2}"));
        Assert.Equal(h1, h2);
        Assert.NotEqual(h1, h3);
        Assert.Equal(64, h1.Length); // SHA-256 hex
    }

    [Fact]
    public void Canonicalize_Null_IsHandled()
    {
        Assert.Equal("null", CanonicalJson.Canonicalize("null"));
    }
}
