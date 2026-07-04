using System.Text.Json;
using System.Text.Json.Serialization;
using Sek.Core.Model;

namespace Sek.Core.Seexpl;

/// <summary>
/// The on-disk <c>.seexpl</c> exploration format for SpecExplorerKit: a modern,
/// human-diffable JSON serialization of an <see cref="ExplorationGraph"/>. (The
/// classic Spec Explorer <c>.seexpl</c> was a proprietary binary/XML format; SEK
/// defines its own JSON schema so explorations can be viewed and version-controlled
/// without Visual Studio.)
/// </summary>
public sealed class SeexplDocument
{
    [JsonPropertyName("seexplVersion")]
    public string SeexplVersion { get; set; } = "0.1";

    [JsonPropertyName("machine")]
    public string Machine { get; set; } = string.Empty;

    [JsonPropertyName("generatedUtc")]
    public DateTime GeneratedUtc { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("initialState")]
    public string? InitialState { get; set; }

    [JsonPropertyName("states")]
    public List<SeexplState> States { get; set; } = new();

    [JsonPropertyName("transitions")]
    public List<SeexplTransition> Transitions { get; set; } = new();

    [JsonPropertyName("metadata")]
    public Dictionary<string, string> Metadata { get; set; } = new();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static SeexplDocument FromGraph(ExplorationGraph graph)
    {
        var doc = new SeexplDocument
        {
            Machine = graph.Machine,
            InitialState = graph.InitialStateId,
            Metadata = new Dictionary<string, string>(graph.Metadata),
        };

        foreach (var s in graph.States)
        {
            doc.States.Add(new SeexplState
            {
                Id = s.Id,
                Hash = s.Hash,
                Label = s.Label,
                Accepting = s.Accepting,
                Initial = s.Initial,
            });
        }

        foreach (var t in graph.Transitions)
        {
            doc.Transitions.Add(new SeexplTransition
            {
                From = t.FromStateId,
                To = t.ToStateId,
                Action = t.Action.Name,
                Arguments = t.Action.Arguments.ToList(),
            });
        }

        return doc;
    }

    public ExplorationGraph ToGraph()
    {
        var graph = new ExplorationGraph { Machine = Machine, InitialStateId = InitialState };
        foreach (var kv in Metadata)
        {
            graph.Metadata[kv.Key] = kv.Value;
        }

        foreach (var s in States)
        {
            graph.States.Add(new ModelState(s.Id, s.Hash ?? string.Empty, s.Label, s.Accepting, s.Initial));
        }

        foreach (var t in Transitions)
        {
            graph.Transitions.Add(new Transition(
                t.From,
                new ActionInvocation(t.Action, t.Arguments ?? new List<string>()),
                t.To));
        }

        return graph;
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public void Save(string path) => File.WriteAllText(path, ToJson());

    public static SeexplDocument Load(string path)
    {
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<SeexplDocument>(json, JsonOptions)
               ?? throw new InvalidDataException($"'{path}' is not a valid .seexpl document.");
    }
}

public sealed class SeexplState
{
    [JsonPropertyName("id")] public string Id { get; set; } = string.Empty;
    [JsonPropertyName("hash")] public string? Hash { get; set; }
    [JsonPropertyName("label")] public string? Label { get; set; }
    [JsonPropertyName("accepting")] public bool Accepting { get; set; }
    [JsonPropertyName("initial")] public bool Initial { get; set; }
}

public sealed class SeexplTransition
{
    [JsonPropertyName("from")] public string From { get; set; } = string.Empty;
    [JsonPropertyName("to")] public string To { get; set; } = string.Empty;
    [JsonPropertyName("action")] public string Action { get; set; } = string.Empty;
    [JsonPropertyName("arguments")] public List<string>? Arguments { get; set; }
}
