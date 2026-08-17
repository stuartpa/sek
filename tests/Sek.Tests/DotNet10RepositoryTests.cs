using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Sek.Tests;

public sealed class DotNet10RepositoryTests
{
    private static readonly string Root = FindRepoRoot();

    [Fact]
    public void Repository_isPinnedToDotNet10Only()
    {
        using var global = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "global.json")));
        var sdk = global.RootElement.GetProperty("sdk");
        Assert.Equal("10.0.303", sdk.GetProperty("version").GetString());
        Assert.Equal("disable", sdk.GetProperty("rollForward").GetString());
        Assert.False(sdk.GetProperty("allowPrerelease").GetBoolean());

        var props = File.ReadAllText(Path.Combine(Root, "Directory.Build.props"));
        Assert.Contains("<TargetFramework>net10.0</TargetFramework>", props, StringComparison.Ordinal);
        Assert.DoesNotContain(LegacyNet(8), props, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(LegacyNet(9), props, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>", props, StringComparison.Ordinal);

        var generated = File.ReadAllText(Path.Combine(Root, "src", "Sek.Cli", "TestGen.cs"));
        Assert.Contains("<TargetFramework>net10.0</TargetFramework>", generated, StringComparison.Ordinal);
        Assert.DoesNotContain($"<TargetFramework>{LegacyNet(8)}</TargetFramework>", generated, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain($"<TargetFramework>{LegacyNet(9)}</TargetFramework>", generated, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ModernSlnx_containsEveryActiveProjectAndNoLegacySolutionExists()
    {
        var tracked = TrackedFiles();
        Assert.DoesNotContain(tracked, path => path.EndsWith(".sln", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(tracked, path => path.EndsWith(".testrunconfig", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(tracked, path => path.StartsWith("samples-source/", StringComparison.Ordinal) &&
                                               (path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ||
                                                path.EndsWith(".vcxproj", StringComparison.OrdinalIgnoreCase)));

        var activeProjects = tracked
            .Where(path => path.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        Assert.NotEmpty(activeProjects);

        var solution = File.ReadAllText(Path.Combine(Root, "Sek.slnx"));
        var solutionProjects = Regex.Matches(solution, @"<Project Path=""(?<path>[^""]+\.csproj)""", RegexOptions.CultureInvariant)
            .Select(match => match.Groups["path"].Value.Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        Assert.Equal(activeProjects, solutionProjects);
    }

    [Fact]
    public void TrackedProductText_containsNoDotNet8OrDotNet9Contract()
    {
        var forbidden = new[]
        {
            LegacyNet(8), LegacyNet(9), LegacyProduct(8), LegacyProduct(9),
            "dotnet-version: \"" + 8, "dotnet-version: \"" + 9,
            "version: \">=" + 8 + ".0", "version: \">=" + 9 + ".0",
        };
        var textExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".cs", ".csproj", ".props", ".targets", ".json", ".yml", ".yaml", ".md", ".ps1", ".sh", ".slnx",
        };

        var failures = new List<string>();
        foreach (var relative in TrackedFiles())
        {
            if (relative == "tests/Sek.Tests/DotNet10RepositoryTests.cs") continue;
            if (!textExtensions.Contains(Path.GetExtension(relative))) continue;
            var full = Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));
            var text = File.ReadAllText(full);
            foreach (var marker in forbidden)
            {
                var index = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (index >= 0) failures.Add($"{relative}:{LineOf(text, index)}:{marker}");
            }
        }

        Assert.True(failures.Count == 0, "Tracked legacy framework references remain:\\n" + string.Join("\n", failures));
        var unqualifiedOutput = new Regex(
            @"bin[/\\]Debug[/\\](?!net10\.0(?:[/\\]|(?=[^A-Za-z0-9.]|$)))",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var outputFailures = TrackedFiles()
            .Where(relative => relative != "tests/Sek.Tests/DotNet10RepositoryTests.cs")
            .Where(relative => File.Exists(Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar))))
            .Where(relative => unqualifiedOutput.IsMatch(File.ReadAllText(Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar)))))
            .ToArray();
        Assert.True(outputFailures.Length == 0, "Unqualified Debug output paths remain:\n" + string.Join("\n", outputFailures));
    }

    [Fact]
    public void WorkflowsAndExtensionRequireDotNet10()
    {
        foreach (var workflow in new[] { "ci.yml", "docs.yml", "release.yml" })
        {
            var text = File.ReadAllText(Path.Combine(Root, ".github", "workflows", workflow));
            Assert.Contains("dotnet-version: \"10.0.303\"", text, StringComparison.Ordinal);
        }

        var extension = File.ReadAllText(Path.Combine(Root, "extensions", "spec-kit-sek", "extension.yml"));
        Assert.Contains("version: \">=10.0\"", extension, StringComparison.Ordinal);
        var catalog = File.ReadAllText(Path.Combine(Root, "extensions", "catalog.community.json"));
        Assert.Contains("\"version\": \">=10.0\"", catalog, StringComparison.Ordinal);
    }

    private static string LegacyNet(int major) => "net" + major + ".0";
    private static string LegacyProduct(int major) => ".NET " + major;
    private static int LineOf(string text, int index)
        => text.AsSpan(0, index).Count('\n') + 1;

    private static string[] TrackedFiles()
    {
        var start = new ProcessStartInfo("git")
        {
            WorkingDirectory = Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        start.ArgumentList.Add("ls-files");
        start.ArgumentList.Add("-z");
        using var process = Process.Start(start) ?? throw new InvalidOperationException("git-start-failed");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, error);
        return output.Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Select(path => path.Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Sek.slnx")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        return directory!.FullName;
    }
}
