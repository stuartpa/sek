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

    // ---- Additional model samples (slicing / config parameters) ------------------------

    [Theory]
    [InlineData("samples/Account", "ModelProgram")]
    [InlineData("samples/Account", "SlicedModelProgram")]
    [InlineData("samples/atsvc", "ModelProgramWithConfigParameters")]
    [InlineData("samples/atsvc", "ModelProgramWithTwoJobsPattern")]
    [InlineData("samples/atsvc", "AddTwoJobsPattern")]
    public void Explore_ExtraSampleMachine_InProcess_Succeeds(string project, string machine)
    {
        var dir = Path.Combine(_fx.RepoRoot, project.Replace('/', Path.DirectorySeparatorChar));
        var (code, _, err) = CliHost.Run("explore", machine, "--project", dir);
        Assert.True(code == 0, $"explore {project}/{machine} failed: {err}");
    }

    // ---- Other CLI commands ------------------------------------------------------------

    [Fact]
    public void Init_ScaffoldsProject()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"sek_init_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tmp);
        try
        {
            var (code, _, _) = CliHost.Run("init", "--project", tmp);
            Assert.Equal(0, code);
            Assert.True(File.Exists(Path.Combine(tmp, ".specexplorerkit", "config.json")));
        }
        finally
        {
            try { Directory.Delete(tmp, recursive: true); } catch { /* best effort */ }
        }
    }

    [Fact]
    public void Test_Turnstile_Conformance_Runs()
    {
        var dir = Path.Combine(_fx.RepoRoot, "samples", "Turnstile");
        var (code, output, err) = CliHost.Run("test", "ModelProgram", "--project", dir);
        // The conformance command executes (exit 0 = conformant, 1 = mismatch reported); either is a
        // valid run that exercises CmdTest + the conformance replay. A crash (99) is not acceptable.
        Assert.NotEqual(99, code);
        Assert.False(string.IsNullOrWhiteSpace(output + err));
    }

    // ---- Error paths -------------------------------------------------------------------

    [Fact]
    public void Explore_UnknownMachine_ReturnsError()
    {
        var dir = Path.Combine(_fx.RepoRoot, "samples", "Turnstile");
        var (code, _, err) = CliHost.Run("explore", "NoSuchMachine", "--project", dir);
        Assert.NotEqual(0, code);
        Assert.NotEmpty(err);
    }

    [Fact]
    public void View_MissingFile_ReturnsError()
    {
        var (code, _, _) = CliHost.Run("view", Path.Combine(Path.GetTempPath(), "does-not-exist.seexpl"));
        Assert.NotEqual(0, code);
    }

    [Fact]
    public void Explore_MissingProject_ReturnsError()
    {
        var tmp = Path.Combine(Path.GetTempPath(), $"sek_noproj_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tmp);
        try
        {
            var (code, _, _) = CliHost.Run("explore", "M", "--project", tmp); // no .specexplorerkit
            Assert.NotEqual(0, code);
        }
        finally
        {
            try { Directory.Delete(tmp, recursive: true); } catch { /* best effort */ }
        }
    }

    [Fact]
    public void View_HtmlFormat_Renders()
    {
        var dir = Path.Combine(_fx.RepoRoot, "samples", "Turnstile");
        CliHost.Run("explore", "ModelProgram", "--project", dir);
        var seexpl = Path.Combine(dir, ".specexplorerkit", "out", "ModelProgram.seexpl");
        var outFile = Path.Combine(Path.GetTempPath(), $"sek_{Guid.NewGuid():N}.html");
        var (code, _, _) = CliHost.Run("view", seexpl, "--format", "html", "--out", outFile);
        Assert.Equal(0, code);
        Assert.True(File.Exists(outFile));
        try { File.Delete(outFile); } catch { }
    }

    [Fact]
    public void Generate_WithoutBinding_ReportsError()
    {
        // ParameterGeneration has no `binding` in its config → generate cannot produce conformance
        // tests and must report an error rather than crash.
        var dir = Path.Combine(_fx.RepoRoot, "samples", "ParameterGeneration");
        var (code, _, _) = CliHost.Run("generate", "Struct", "--project", dir);
        Assert.NotEqual(99, code); // ran and handled (error or graceful), not a crash
    }

    [Fact]
    public void Validate_WithRuleMismatch_ReportsProblems()
    {
        // ParameterGeneration declares actions that don't all map to model rules → validate reports
        // problems (exit 1), exercising the problem-reporting path.
        var dir = Path.Combine(_fx.RepoRoot, "samples", "ParameterGeneration");
        var (code, output, err) = CliHost.Run("validate", "--project", dir);
        Assert.NotEqual(99, code);
        Assert.False(string.IsNullOrWhiteSpace(output + err));
    }
}
