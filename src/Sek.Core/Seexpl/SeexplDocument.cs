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
                ObligationId = n.ObligationId,
                ExpectedExceptionType = n.ExpectedExceptionType,
                ExpectedMessageContains = n.ExpectedMessageContains,
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
                n.Reason ?? string.Empty)
            {
                ObligationId = n.ObligationId,
                ExpectedExceptionType = n.ExpectedExceptionType,
                ExpectedMessageContains = n.ExpectedMessageContains,
            });
        }

        return graph;
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public void Save(string path) => Save(path, DefaultMaxBytes);

    public void Save(string path, long maxBytes, CancellationToken cancellationToken = default) =>
        Publish(path, maxBytes, cancellationToken, stream => JsonSerializer.Serialize(stream, this, JsonOptions));

    private const long DefaultMaxBytes = 25 * 1024 * 1024;

    public static void SaveGraph(ExplorationGraph graph, string path, long maxBytes = DefaultMaxBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(graph);
        Publish(path, maxBytes, cancellationToken, stream =>
        {
            using var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });
            writer.WriteStartObject();
            writer.WriteString("seexplVersion", "0.1");
            writer.WriteString("machine", graph.Machine);
            writer.WriteString("generatedUtc", DateTime.UtcNow);
            if (graph.InitialStateId is not null) writer.WriteString("initialState", graph.InitialStateId);
            writer.WriteStartArray("states");
            foreach (var state in graph.States)
            {
                cancellationToken.ThrowIfCancellationRequested();
                writer.WriteStartObject();
                writer.WriteString("id", state.Id);
                if (state.Hash is not null) writer.WriteString("hash", state.Hash);
                if (state.Label is not null) writer.WriteString("label", state.Label);
                writer.WriteBoolean("accepting", state.Accepting);
                writer.WriteBoolean("initial", state.Initial);
                writer.WriteEndObject();
                writer.Flush();
            }
            writer.WriteEndArray();
            writer.WriteStartArray("transitions");
            foreach (var transition in graph.Transitions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                writer.WriteStartObject();
                writer.WriteString("from", transition.FromStateId);
                writer.WriteString("to", transition.ToStateId);
                writer.WriteString("action", transition.Action.Name);
                writer.WritePropertyName("arguments");
                JsonSerializer.Serialize(writer, transition.Action.Arguments, JsonOptions);
                if (transition.Action.Kind != "call" && transition.Action.Kind is not null)
                    writer.WriteString("kind", transition.Action.Kind);
                if (transition.Action.Result is not null) writer.WriteString("result", transition.Action.Result);
                writer.WriteEndObject();
                writer.Flush();
            }
            writer.WriteEndArray();
            writer.WriteStartArray("negativeTransitions");
            foreach (var transition in graph.NegativeTransitions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                writer.WriteStartObject();
                writer.WriteString("from", transition.FromStateId);
                writer.WriteString("action", transition.Action.Name);
                if (transition.Action.Arguments.Count != 0)
                {
                    writer.WritePropertyName("arguments");
                    JsonSerializer.Serialize(writer, transition.Action.Arguments, JsonOptions);
                }
                if (transition.Reason is not null) writer.WriteString("reason", transition.Reason);
                if (transition.ObligationId is not null) writer.WriteString("obligationId", transition.ObligationId);
                if (transition.ExpectedExceptionType is not null) writer.WriteString("expectedExceptionType", transition.ExpectedExceptionType);
                if (transition.ExpectedMessageContains is not null) writer.WriteString("expectedMessageContains", transition.ExpectedMessageContains);
                writer.WriteEndObject();
                writer.Flush();
            }
            writer.WriteEndArray();
            writer.WriteStartObject("metadata");
            foreach (var entry in graph.Metadata)
            {
                cancellationToken.ThrowIfCancellationRequested();
                writer.WritePropertyName(entry.Key);
                JsonSerializer.Serialize(writer, entry.Value, JsonOptions);
                writer.Flush();
            }
            writer.WriteEndObject();
            writer.WriteEndObject();
            writer.Flush();
        });
    }

    private static void Publish(string path, long maxBytes, CancellationToken cancellationToken, Action<Stream> write)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBytes);
        cancellationToken.ThrowIfCancellationRequested();
        var fullPath = Path.GetFullPath(path);
        var tempPath = fullPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        var ownsTemp = false;
        try
        {
            using (var file = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                ownsTemp = true;
                using var stream = new BoundedWriteStream(file, maxBytes, cancellationToken);
                write(stream);
                stream.Flush();
            }
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(tempPath, fullPath, overwrite: true);
        }
        finally
        {
            if (ownsTemp && File.Exists(tempPath)) File.Delete(tempPath);
        }
    }

    public static SeexplDocument Load(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > DefaultMaxBytes)
            throw new InvalidDataException($"'{path}' exceeds the .seexpl size limit of {DefaultMaxBytes} bytes.");
        return JsonSerializer.Deserialize<SeexplDocument>(stream, JsonOptions)
               ?? throw new InvalidDataException($"'{path}' is not a valid .seexpl document.");
    }

    private sealed class BoundedWriteStream(Stream inner, long maxBytes, CancellationToken cancellationToken) : Stream
    {
        private long written;

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => written;
        public override long Position { get => written; set => throw new NotSupportedException(); }

        public override void Flush()
        {
            cancellationToken.ThrowIfCancellationRequested();
            inner.Flush();
        }

        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (buffer.Length > maxBytes - written)
                throw new InvalidDataException($".seexpl serialization exceeds the size limit of {maxBytes} bytes.");
            inner.Write(buffer);
            written += buffer.Length;
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
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
    [JsonPropertyName("obligationId")] public string? ObligationId { get; set; }
    [JsonPropertyName("expectedExceptionType")] public string? ExpectedExceptionType { get; set; }
    [JsonPropertyName("expectedMessageContains")] public string? ExpectedMessageContains { get; set; }
}
