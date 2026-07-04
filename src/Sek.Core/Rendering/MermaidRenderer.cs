using System.Text;
using Sek.Core.Model;

namespace Sek.Core.Rendering;

/// <summary>Renders an <see cref="ExplorationGraph"/> as a Mermaid state diagram.</summary>
public static class MermaidRenderer
{
    public static string Render(ExplorationGraph graph)
    {
        var sb = new StringBuilder();
        sb.AppendLine("stateDiagram-v2");

        if (graph.InitialStateId is { } init)
        {
            sb.AppendLine($"    [*] --> {Safe(init)}");
        }

        foreach (var s in graph.States)
        {
            var label = string.IsNullOrEmpty(s.Label) ? s.Id : $"{s.Id}: {s.Label}";
            sb.AppendLine($"    {Safe(s.Id)} : {Escape(label)}");
            if (s.Accepting)
            {
                sb.AppendLine($"    {Safe(s.Id)} --> [*]");
            }
        }

        foreach (var t in graph.Transitions)
        {
            sb.AppendLine($"    {Safe(t.FromStateId)} --> {Safe(t.ToStateId)} : {Escape(t.Action.Display)}");
        }

        return sb.ToString();
    }

    private static string Safe(string id) => id.Replace("-", "_").Replace(" ", "_");

    private static string Escape(string text) => text.Replace("\"", "'").Replace("\n", " ");
}
