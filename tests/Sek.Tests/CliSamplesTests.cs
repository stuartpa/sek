using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// Builds the sample model assemblies once, so in-process CLI runs against the samples can load them.
/// </summary>
public sealed class SampleModelsFixture
{
    public string RepoRoot { get; } = CliHost.RepoRoot();

    public SampleModelsFixture()
    {
        var samples = Path.Combine(RepoRoot, "samples");
        if (!Directory.Exists(samples)) return;

        // Each sample model now has a unique assembly name, so all can be co-loaded in one process.
        foreach (var csproj in Directory.EnumerateFiles(samples, "*.Model.csproj", SearchOption.AllDirectories))
        {
            Build(csproj);
        }

        var sut = Path.Combine(samples, "Turnstile", "Sut", "Turnstile.Sut.csproj");
        if (File.Exists(sut)) Build(sut);
    }

    private static void Build(string csproj)
    {
        if (!File.Exists(csproj)) return;
        var psi = new ProcessStartInfo("dotnet", $"build \"{csproj}\" -c Debug --nologo -v q")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var p = Process.Start(psi)!;
        p.WaitForExit();
    }
}

/// <summary>
/// Integration coverage for the <c>sek</c> CLI + engine: drives every sample exploration in the
/// regression manifest through the in-process CLI (covering <see cref="Sek.Engine.Explorer"/>'s
/// parameter/struct/combination/slice/point-shoot/requirement paths and the behavior automaton),
/// plus validate / view / generate.
/// </summary>
public class CliSamplesTests : IClassFixture<SampleModelsFixture>
{
    private readonly SampleModelsFixture _fx;
    public CliSamplesTests(SampleModelsFixture fx) => _fx = fx;

    public static IEnumerable<object[]> ManifestEntries()
    {
        // Every sample model now has a unique assembly name, so all can be driven in one process.
        var root = CliHost.RepoRoot();
        var manifest = Path.Combine(root, "samples", "regression.manifest.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(manifest));
        foreach (var e in doc.RootElement.GetProperty("entries").EnumerateArray())
        {
            var project = e.GetProperty("project").GetString()!;
            var machine = e.GetProperty("machine").GetString()!;
            yield return new object[] { project, machine };
        }
    }

    [Theory]
    [MemberData(nameof(ManifestEntries))]
    public void Explore_SampleMachine_InProcess_Succeeds(string project, string machine)
    {
        var dir = Path.Combine(_fx.RepoRoot, project.Replace('/', Path.DirectorySeparatorChar));
        var (code, _, err) = CliHost.Run("explore", machine, "--project", dir);
        Assert.True(code == 0, $"explore {project}/{machine} failed: {err}");

        // A .seexpl graph was produced and is loadable.
        var seexpl = Path.Combine(dir, ".specexplorerkit", "out", machine + ".seexpl");
        Assert.True(File.Exists(seexpl), $"no .seexpl produced for {project}/{machine}");
    }

    [Fact]
    public void Validate_Turnstile_Succeeds()
    {
        var dir = Path.Combine(_fx.RepoRoot, "samples", "Turnstile");
        var (code, output, _) = CliHost.Run("validate", "--project", dir);
        Assert.Equal(0, code);
        Assert.Contains("validate: OK", output);
    }

    [Fact]
    public void View_ProducedSeexpl_RendersHtmlAndDot()
    {
        var dir = Path.Combine(_fx.RepoRoot, "samples", "Turnstile");
        CliHost.Run("explore", "ModelProgram", "--project", dir);
        var seexpl = Path.Combine(dir, ".specexplorerkit", "out", "ModelProgram.seexpl");

        var dotOut = Path.Combine(Path.GetTempPath(), $"sek_{Guid.NewGuid():N}.dot");
        var mmdOut = Path.Combine(Path.GetTempPath(), $"sek_{Guid.NewGuid():N}.mmd");
        var (dotCode, _, _) = CliHost.Run("view", seexpl, "--format", "dot", "--out", dotOut);
        Assert.Equal(0, dotCode);
        var (mmdCode, _, _) = CliHost.Run("view", seexpl, "--format", "mermaid", "--out", mmdOut);
        Assert.Equal(0, mmdCode);
    }

    [Fact]
    public void Generate_Turnstile_ProducesTestProject()
    {
        var dir = Path.Combine(_fx.RepoRoot, "samples", "Turnstile");
        var (code, _, err) = CliHost.Run("generate", "ModelProgram", "--project", dir);
        Assert.True(code == 0, $"generate failed: {err}");
    }
}
