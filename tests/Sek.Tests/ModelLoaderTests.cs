using System;
using System.IO;
using Sek.Cli;
using Xunit;

namespace Sek.Tests;

/// <summary>
/// Coverage for <see cref="ModelLoader"/> — loading a model type from a built assembly, scope
/// (namespace) resolution, and the error paths. Uses the fixture-built SelfHost model assembly.
/// </summary>
public class ModelLoaderTests : IClassFixture<SampleModelsFixture>
{
    private readonly string _asm;

    public ModelLoaderTests(SampleModelsFixture fx)
    {
        _asm = Path.Combine(fx.RepoRoot, "samples", "SelfHost", "Model", "bin", "Debug", "SelfHost.Model.dll");
    }

    [Fact]
    public void LoadModelType_ReturnsType()
    {
        var t = ModelLoader.LoadModelType(_asm, "SelfHost.Model.SekWorkflowModel");
        Assert.Equal("SelfHost.Model.SekWorkflowModel", t.FullName);
    }

    [Fact]
    public void LoadModelType_UnknownType_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => ModelLoader.LoadModelType(_asm, "No.Such.Type"));
    }

    [Fact]
    public void LoadModelType_MissingAssembly_Throws()
    {
        Assert.Throws<FileNotFoundException>(() =>
            ModelLoader.LoadModelType(Path.Combine(Path.GetTempPath(), "no-such.dll"), "X"));
    }

    [Fact]
    public void LoadModelTypeInScope_EmptyScope_FallsBackToDefault()
    {
        var t = ModelLoader.LoadModelTypeInScope(_asm, null, "SelfHost.Model.SekWorkflowModel");
        Assert.Equal("SelfHost.Model.SekWorkflowModel", t.FullName);
    }

    [Fact]
    public void LoadModelTypeInScope_MatchingNamespace_ResolvesSingle()
    {
        var t = ModelLoader.LoadModelTypeInScope(_asm, "SelfHost.Model", "irrelevant");
        Assert.Equal("SelfHost.Model.SekWorkflowModel", t.FullName);
    }

    [Fact]
    public void LoadModelTypeInScope_UnknownScope_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            ModelLoader.LoadModelTypeInScope(_asm, "No.Such.Namespace", "SelfHost.Model.SekWorkflowModel"));
    }

    [Fact]
    public void LoadModelTypeInScope_MissingAssembly_Throws()
    {
        Assert.Throws<FileNotFoundException>(() =>
            ModelLoader.LoadModelTypeInScope(Path.Combine(Path.GetTempPath(), "no-such.dll"), "Ns", "T"));
    }
}
