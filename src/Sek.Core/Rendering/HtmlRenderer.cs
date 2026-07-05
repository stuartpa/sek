using System.Text;
using Sek.Core.Model;

namespace Sek.Core.Rendering;

/// <summary>
/// Renders an <see cref="ExplorationGraph"/> as a self-contained HTML page that draws
/// the state diagram with Mermaid. Suitable for opening in a browser or the VS Code
/// Simple Browser / Live Preview.
/// </summary>
public static class HtmlRenderer
{
    public static string Render(ExplorationGraph graph)
    {
        var mermaid = MermaidRenderer.Render(graph);
        var title = string.IsNullOrEmpty(graph.Machine) ? "SEK Exploration" : graph.Machine;

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"utf-8\" />");
        sb.AppendLine($"  <title>{Html(title)} — SEK exploration</title>");
        sb.AppendLine("  <script src=\"https://cdn.jsdelivr.net/npm/mermaid/dist/mermaid.min.js\"></script>");
        sb.AppendLine("  <style>");
        sb.AppendLine("    body { font-family: 'Segoe UI', system-ui, sans-serif; margin: 1.5rem; }");
        sb.AppendLine("    h1 { font-size: 1.1rem; }");
        sb.AppendLine("    .meta { color: #666; font-size: 0.85rem; margin-bottom: 1rem; }");
        sb.AppendLine("    .mermaid { border: 1px solid #ddd; border-radius: 6px; padding: 1rem; }");
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine($"  <h1>{Html(title)}</h1>");
        sb.AppendLine($"  <div class=\"meta\">{graph.States.Count} states, {graph.Transitions.Count} transitions</div>");
        if (graph.Transitions.Count > 2000)
        {
            sb.AppendLine("  <div class=\"meta\">Large graph — Mermaid may take a while to lay out; the Graphviz DOT output (<code>--format dot</code>) scales better for graphs this size.</div>");
        }

        sb.AppendLine("  <pre class=\"mermaid\">");
        sb.Append(Html(mermaid));
        sb.AppendLine("  </pre>");
        // Raise Mermaid's default caps (maxTextSize 50k / maxEdges 500) so large exploration
        // graphs render instead of failing with "Maximum text size in diagram exceeded".
        sb.AppendLine("  <script>mermaid.initialize({ startOnLoad: true, securityLevel: 'loose', maxTextSize: 90000000, maxEdges: 500000 });</script>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    private static string Html(string text) =>
        text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
