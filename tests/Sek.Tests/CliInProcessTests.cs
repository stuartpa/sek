using Xunit;

namespace Sek.Tests;

/// <summary>
/// Drives the <c>sek</c> CLI <em>in-process</em> (via <see cref="CliHost"/>) for the simple,
/// project-less commands, so their coverage is captured by the test host.
/// </summary>
public class CliInProcessTests
{
    private static (int Code, string Out, string Err) Run(params string[] args) => CliHost.Run(args);

    [Fact]
    public void Version_PrintsVersion()
    {
        var (code, output, _) = Run("version");
        Assert.Equal(0, code);
        Assert.Contains("0.1", output);
    }

    [Fact]
    public void NoArgs_PrintsUsage()
    {
        var (code, output, _) = Run();
        Assert.Equal(0, code);
        Assert.Contains("explore", output);
    }

    [Fact]
    public void Help_PrintsUsage()
    {
        var (code, output, _) = Run("--help");
        Assert.Equal(0, code);
        Assert.Contains("sek", output.ToLowerInvariant());
    }

    [Fact]
    public void UnknownCommand_ReturnsError()
    {
        var (code, _, _) = Run("bogus-command");
        Assert.NotEqual(0, code);
    }

    [Fact]
    public void Z3_SelfTest_Runs()
    {
        var (code, output, _) = Run("z3");
        Assert.Equal(0, code);
        Assert.Contains("Z3", output);
    }
}
