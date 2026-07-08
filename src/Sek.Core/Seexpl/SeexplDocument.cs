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

    [JsonPropertyName("negativeTransitions")]
    public List<SeexplNegativeTransition> NegativeTransitions { get; set; } = new();

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
                Kind = t.Action.Kind == "call" ? null : t.Action.Kind,
                Result = t.Action.Result,
            });
        }

        foreach (var n in graph.NegativeTransitions)
        {
            doc.NegativeTransitions.Add(new SeexplNegativeTransition
            {
                From = n.FromStateId,
                Action = n.Action.Name,
                Arguments = n.Action.Arguments.Count == 0 ? null : n.Action.Arguments.ToList(),
                Reason = n.Reason,
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
                new ActionInvocation(t.Action, t.Arguments ?? new List<string>(), t.Kind ?? "call", t.Result),
                t.To));
        }

        foreach (var n in NegativeTransitions)
        {
            graph.NegativeTransitions.Add(new NegativeTransition(
                n.From,
                new ActionInvocation(n.Action, n.Arguments ?? new List<string>()),
                n.Reason ?? string.Empty));
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
    [JsonPropertyName("kind")] public string? Kind { get; set; }
    [JsonPropertyName("result")] public string? Result { get; set; }
}

/// <summary>A model-derived illegal (state, action) pair persisted in the <c>.seexpl</c>: the action
/// is forbidden from <see cref="From"/> (its guard is false there) and a SUT must reject it.</summary>
public sealed class SeexplNegativeTransition
{
    [JsonPropertyName("from")] public string From { get; set; } = string.Empty;
    [JsonPropertyName("action")] public string Action { get; set; } = string.Empty;
    [JsonPropertyName("arguments")] public List<string>? Arguments { get; set; }
    [JsonPropertyName("reason")] public string? Reason { get; set; }
}
