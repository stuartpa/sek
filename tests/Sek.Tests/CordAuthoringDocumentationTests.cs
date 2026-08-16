using Sek.Cord;
using Xunit;

namespace Sek.Tests;

public sealed class CordAuthoringDocumentationTests
{
    private static readonly string Root = FindRepoRoot();
    private static readonly string ExtensionRoot = Path.Combine(Root, "extensions", "spec-kit-sek");
    private static readonly string SkillRoot = Path.Combine(ExtensionRoot, "skills", "sek-cord-authoring");

    [Fact]
    public void CordSkill_isOwnedBySekProgressiveAndComplete()
    {
        var skillPath = Path.Combine(SkillRoot, "SKILL.md");
        var skill = File.ReadAllText(skillPath);
        Assert.Contains("name: sek-cord-authoring", skill, StringComparison.Ordinal);
        Assert.Contains("./references/implemented-language.md", skill, StringComparison.Ordinal);
        Assert.Contains("./references/operator-semantics.md", skill, StringComparison.Ordinal);
        Assert.Contains("./references/authoring-patterns.md", skill, StringComparison.Ordinal);
        Assert.Contains("./references/support-and-safety.md", skill, StringComparison.Ordinal);
        Assert.InRange(File.ReadAllLines(skillPath).Length, 1, 500);

        var referenceRoot = Path.Combine(SkillRoot, "references");
        var references = new[]
        {
            "implemented-language.md",
            "operator-semantics.md",
            "authoring-patterns.md",
            "support-and-safety.md",
        };
        foreach (var reference in references)
            Assert.True(File.Exists(Path.Combine(referenceRoot, reference)), reference);

        var completeReference = string.Join("\n", references.Select(reference =>
            File.ReadAllText(Path.Combine(referenceRoot, reference))));
        foreach (var marker in new[]
        {
            "Condition.In", "Condition.IsTrue", "Combination.Seeded", "Probability.IsTrue",
            "bind", "let", "construct point shoot", "construct requirement coverage",
            "|||", "|?|", ": fail", "Parsed-only", "Unsupported",
        })
            Assert.Contains(marker, completeReference, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryBundledCordAsset_parsesWithTheCurrentSekFrontend()
    {
        var assets = Directory.GetFiles(Path.Combine(SkillRoot, "assets"), "*.cord");
        Assert.Equal(3, assets.Length);
        foreach (var asset in assets)
        {
            var exception = Record.Exception(() => CordDocument.ParseText(File.ReadAllText(asset)));
            Assert.True(exception is null, $"{Path.GetFileName(asset)} failed: {exception}");
        }
    }

    [Fact]
    public void OperatorDocumentation_teachesTraceSemanticsNotOnlyOperatorNames()
    {
        var agentReference = File.ReadAllText(Path.Combine(SkillRoot, "references", "operator-semantics.md"));
        var publicReference = File.ReadAllText(Path.Combine(Root, "docs", "reference", "cord-operators.md"));
        foreach (var text in new[] { agentReference, publicReference })
        {
            foreach (var marker in new[]
            {
                "accepted finite", "reachable **prefix", "Initial state accepting?",
                "full signature", "same-label", "!A(1)", "does **not** produce all six",
                "A B C", "A C B", "C A B", "signature-based", "external",
            })
                Assert.Contains(marker, text, StringComparison.OrdinalIgnoreCase);
        }

        var implemented = File.ReadAllText(Path.Combine(SkillRoot, "references", "implemented-language.md"));
        Assert.Contains("operator-semantics.md", implemented, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtensionIdentity_matchesTheSpeckitSekCommandNamespace()
    {
        var manifest = File.ReadAllText(Path.Combine(ExtensionRoot, "extension.yml"));
        Assert.Contains("id: \"sek\"", manifest, StringComparison.Ordinal);
        Assert.DoesNotContain("id: \"spec-kit-sek\"", manifest, StringComparison.Ordinal);
        foreach (var command in new[] { "model", "explore", "verify" })
            Assert.Contains($"name: \"speckit.sek.{command}\"", manifest, StringComparison.Ordinal);

        var catalog = File.ReadAllText(Path.Combine(Root, "extensions", "catalog.community.json"));
        Assert.Contains("\"id\": \"sek\"", catalog, StringComparison.Ordinal);
    }

    [Fact]
    public void ReleaseSurface_packagesTheToolAndModelingRuntimeAtOneVersion()
    {
        var props = File.ReadAllText(Path.Combine(Root, "Directory.Build.props"));
        Assert.Contains("<VersionPrefix>0.1.2</VersionPrefix>", props, StringComparison.Ordinal);

        var cliProject = File.ReadAllText(Path.Combine(Root, "src", "Sek.Cli", "Sek.Cli.csproj"));
        Assert.Contains("<PackageId>SpecExplorerKit.Tool</PackageId>", cliProject, StringComparison.Ordinal);
        Assert.DoesNotContain("<Version>0.1.0</Version>", cliProject, StringComparison.Ordinal);

        var modelingProject = File.ReadAllText(Path.Combine(Root, "src", "Sek.Modeling", "Sek.Modeling.csproj"));
        Assert.Contains("<PackageId>SpecExplorerKit.Modeling</PackageId>", modelingProject, StringComparison.Ordinal);

        var packScript = File.ReadAllText(Path.Combine(Root, "scripts", "pack-extension.ps1"));
        Assert.Contains("src/Sek.Cli/Sek.Cli.csproj", packScript, StringComparison.Ordinal);
        Assert.Contains("src/Sek.Modeling/Sek.Modeling.csproj", packScript, StringComparison.Ordinal);

        var readme = File.ReadAllText(Path.Combine(Root, "README.md"));
        Assert.Contains("dotnet tool install -g SpecExplorerKit.Tool", readme, StringComparison.Ordinal);
        Assert.DoesNotContain("dotnet tool install -g sek ", readme, StringComparison.Ordinal);
    }

    [Fact]
    public void DiscoverableSkill_delegatesToReleasedSekExtension()
    {
        var wrapper = File.ReadAllText(Path.Combine(Root, ".github", "skills", "sek-cord-authoring", "SKILL.md"));
        Assert.Contains("extensions/spec-kit-sek/skills/sek-cord-authoring/SKILL.md", wrapper, StringComparison.Ordinal);
        Assert.DoesNotContain("## Operators", wrapper, StringComparison.Ordinal);
        Assert.InRange(wrapper.Split('\n').Length, 1, 80);

        var consumerWrapper = File.ReadAllText(Path.Combine(Root, ".github", "skills", "using-sek-to-generate-tests", "SKILL.md"));
        Assert.Contains("extensions/spec-kit-sek/skills/using-sek-to-generate-tests/SKILL.md", consumerWrapper, StringComparison.Ordinal);
        var consumer = File.ReadAllText(Path.Combine(ExtensionRoot, "skills", "using-sek-to-generate-tests", "SKILL.md"));
        Assert.Contains("sek-cord-authoring", consumer, StringComparison.Ordinal);
        Assert.Contains("Generated replay contract", consumer, StringComparison.Ordinal);
        Assert.InRange(consumer.Split('\n').Length, 1, 500);
    }

    [Theory]
    [InlineData("model.md")]
    [InlineData("explore.md")]
    public void SpecKitCommands_requireTheSekOwnedCordSkill(string commandFile)
    {
        var command = File.ReadAllText(Path.Combine(ExtensionRoot, "commands", commandFile));
        Assert.Contains("extensions/spec-kit-sek/skills/sek-cord-authoring/SKILL.md", command, StringComparison.Ordinal);
        Assert.Contains(".specify/extensions/sek/skills/sek-cord-authoring/SKILL.md", command, StringComparison.Ordinal);
        Assert.Contains("support-and-safety.md", command, StringComparison.Ordinal);
    }

    [Fact]
    public void VerifyCommand_requiresTheSekOwnedConsumerSkill()
    {
        var command = File.ReadAllText(Path.Combine(ExtensionRoot, "commands", "verify.md"));
        Assert.Contains(".specify/extensions/sek/skills/using-sek-to-generate-tests/SKILL.md", command, StringComparison.Ordinal);
        Assert.Contains("extensions/spec-kit-sek/skills/", command, StringComparison.Ordinal);
        Assert.Contains("do not use a", command, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PublicCordDocumentation_exposesLanguagePatternsAndSupportStatus()
    {
        var reference = File.ReadAllText(Path.Combine(Root, "docs", "reference", "cord-language.md"));
        var operators = File.ReadAllText(Path.Combine(Root, "docs", "reference", "cord-operators.md"));
        var support = File.ReadAllText(Path.Combine(Root, "docs", "reference", "cord-support.md"));
        var guide = File.ReadAllText(Path.Combine(Root, "docs", "guides", "writing-cord.md"));
        Assert.Contains("Behavior precedence", reference, StringComparison.Ordinal);
        Assert.Contains("accepted finite traces", operators, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Supported", support, StringComparison.Ordinal);
        Assert.Contains("Parsed-only", support, StringComparison.Ordinal);
        Assert.Contains("Direct model", guide, StringComparison.Ordinal);
        Assert.Contains("unsliced", guide, StringComparison.OrdinalIgnoreCase);
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
