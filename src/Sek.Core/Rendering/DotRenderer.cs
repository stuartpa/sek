using System.Text;
using Sek.Core.Model;

namespace Sek.Core.Rendering;

/// <summary>Renders an <see cref="ExplorationGraph"/> as Graphviz DOT.</summary>
public static class DotRenderer
{
    public static string Render(ExplorationGraph graph)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"digraph \"{Escape(graph.Machine)}\" {{");
        sb.AppendLine("    rankdir=LR;");
        sb.AppendLine("    node [shape=circle, fontname=\"Segoe UI\"];");
        sb.AppendLine("    edge [fontname=\"Segoe UI\"];");

        foreach (var s in graph.States)
        {
            var shape = s.Accepting ? "doublecircle" : "circle";
            var label = string.IsNullOrEmpty(s.Label) ? s.Id : $"{s.Id}\\n{s.Label}";
            sb.AppendLine($"    \"{Escape(s.Id)}\" [shape={shape}, label=\"{Escape(label)}\"];");
        }

        if (graph.InitialStateId is { } init)
        {
            sb.AppendLine("    __start [shape=point];");
            sb.AppendLine($"    __start -> \"{Escape(init)}\";");
        }

        foreach (var t in graph.Transitions)
        {
            sb.AppendLine($"    \"{Escape(t.FromStateId)}\" -> \"{Escape(t.ToStateId)}\" [label=\"{Escape(t.Action.Display)}\"];");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string Escape(string text) => text.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
