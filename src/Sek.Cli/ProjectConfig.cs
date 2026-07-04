using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sek.Cli;

/// <summary>
/// The <c>.specexplorerkit/config.json</c> project file: where the model assembly, the
/// Cord scripts and the SUT binding live.
/// </summary>
public sealed class ProjectConfig
{
    [JsonPropertyName("model")] public ModelRef Model { get; set; } = new();
    [JsonPropertyName("cord")] public string Cord { get; set; } = "";
    [JsonPropertyName("binding")] public BindingRef? Binding { get; set; }
    [JsonPropertyName("out")] public string Out { get; set; } = ".specexplorerkit/out";

    public sealed class ModelRef
    {
        [JsonPropertyName("assembly")] public string Assembly { get; set; } = "";
        [JsonPropertyName("type")] public string Type { get; set; } = "";
    }

    public sealed class BindingRef
    {
        [JsonPropertyName("assembly")] public string Assembly { get; set; } = "";
        [JsonPropertyName("namespace")] public string Namespace { get; set; } = "";
    }

    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Locate the project directory (containing .specexplorerkit/config.json).</summary>
    public static (ProjectConfig config, string projectDir) Load(string? projectDir)
    {
        var dir = projectDir ?? Directory.GetCurrentDirectory();
        var configPath = Path.Combine(dir, ".specexplorerkit", "config.json");
        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException(
                $"No SpecExplorerKit project found. Expected '{configPath}'. Run `sek init` or pass --project <dir>.");
        }

        var config = JsonSerializer.Deserialize<ProjectConfig>(File.ReadAllText(configPath), Options)
                     ?? throw new InvalidDataException($"Invalid project config: {configPath}");
        return (config, dir);
    }

    public string ResolveModelAssembly(string projectDir) => Path.GetFullPath(Path.Combine(projectDir, Model.Assembly));
    public string ResolveCordDir(string projectDir) => Path.GetFullPath(Path.Combine(projectDir, Cord));
    public string ResolveOutDir(string projectDir) => Path.GetFullPath(Path.Combine(projectDir, Out));
}
