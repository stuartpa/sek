using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sek.Cli;
using Sek.Core.Model;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// Branch coverage for small <c>sek</c> helpers: <see cref="ProjectConfig.Load"/> error paths,
/// <see cref="TestGen.ParseStrategy"/> name variants, and <see cref="TestGen.SelectPaths"/> initial-
/// state fallbacks.
/// </summary>
public class SekBranchCoverageTests
{
    // ---- ProjectConfig.Load --------------------------------------------------------------

    [Fact]
    public void ProjectConfig_Load_NullDir_UsesCwd_AndThrowsWhenAbsent()
    {
        // A temp cwd with no .specexplorerkit → FileNotFound (covers the `projectDir ?? cwd` null arm).
        var prev = Directory.GetCurrentDirectory();
        var tmp = Path.Combine(Path.GetTempPath(), "sekcfg_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmp);
        try
        {
            Directory.SetCurrentDirectory(tmp);
            Assert.Throws<FileNotFoundException>(() => ProjectConfig.Load(null));
        }
        finally
        {
            Directory.SetCurrentDirectory(prev);
            try { Directory.Delete(tmp, true); } catch { }
        }
    }

    [Fact]
    public void ProjectConfig_Load_NullJson_Throws()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sekcfg_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, ".specexplorerkit"));
        File.WriteAllText(Path.Combine(dir, ".specexplorerkit", "config.json"), "null");
        try
        {
            Assert.Throws<InvalidDataException>(() => ProjectConfig.Load(dir));
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    [Fact]
    public void ProjectConfig_Load_ValidConfig_ResolvesPaths()
    {
        var dir = Path.Combine(Path.GetTempPath(), "sekcfg_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, ".specexplorerkit"));
        File.WriteAllText(Path.Combine(dir, ".specexplorerkit", "config.json"),
            "{ \"model\": { \"assembly\": \"m.dll\", \"type\": \"T\" }, \"cord\": \"Model\", \"out\": \"o\" }");
        try
        {
            var (cfg, projDir) = ProjectConfig.Load(dir);
            Assert.Equal(dir, projDir);
            Assert.EndsWith("m.dll", cfg.ResolveModelAssembly(projDir));
            Assert.EndsWith("Model", cfg.ResolveCordDir(projDir));
            Assert.EndsWith("o", cfg.ResolveOutDir(projDir));
            Assert.Null(cfg.Binding);
        }
        finally { try { Directory.Delete(dir, true); } catch { } }
    }

    // ---- TestGen.ParseStrategy -----------------------------------------------------------

    [Theory]
    [InlineData("short", TestGen.TestStrategy.Short)]
    [InlineData("shorttests", TestGen.TestStrategy.Short)]
    [InlineData("  SHORT  ", TestGen.TestStrategy.Short)]
    [InlineData("longtests", TestGen.TestStrategy.Long)]
    [InlineData("anything", TestGen.TestStrategy.Long)]
    [InlineData(null, TestGen.TestStrategy.Long)]
    public void ParseStrategy_MapsNames(string? name, TestGen.TestStrategy expected) =>
        Assert.Equal(expected, TestGen.ParseStrategy(name));

    // ---- TestGen.SelectPaths initial-state fallbacks -------------------------------------

    [Fact]
    public void SelectPaths_InitialStateFallbacks()
    {
        // No InitialStateId, but a state flagged Initial → uses that state.
        var g1 = new ExplorationGraph { Machine = "M", InitialStateId = null };
        g1.States.Add(new ModelState("A", "hA", Label: null, Accepting: false, Initial: true));
        g1.States.Add(new ModelState("B", "hB", Label: null, Accepting: true, Initial: false));
        g1.Transitions.Add(new Transition("A", ActionInvocation.Of("T.a"), "B"));
        Assert.NotEmpty(TestGen.SelectPaths(g1, 5, TestGen.TestStrategy.Long));

        // Neither InitialStateId nor an Initial flag → falls back to "S0".
        var g2 = new ExplorationGraph { Machine = "M", InitialStateId = null };
        g2.States.Add(new ModelState("S0", "h0", Label: null, Accepting: false, Initial: false));
        g2.States.Add(new ModelState("S1", "h1", Label: null, Accepting: true, Initial: false));
        g2.Transitions.Add(new Transition("S0", ActionInvocation.Of("T.a"), "S1"));
        Assert.NotEmpty(TestGen.SelectPaths(g2, 5, TestGen.TestStrategy.Short));
    }
}
