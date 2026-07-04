using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Sek.Engine;

/// <summary>
/// Canonicalizes model-state JSON so that logically-equal states hash identically:
/// object properties are sorted by name and array elements are sorted by their own
/// canonical form (giving set semantics to collections regardless of insertion order).
/// </summary>
public static class CanonicalJson
{
    public static string Canonicalize(string json)
    {
        var node = JsonNode.Parse(json);
        var canonical = Normalize(node);
        return canonical?.ToJsonString() ?? "null";
    }

    public static string Hash(string canonicalJson)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalJson));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static JsonNode? Normalize(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
            {
                var sorted = new JsonObject();
                foreach (var kv in obj.OrderBy(k => k.Key, StringComparer.Ordinal))
                {
                    sorted[kv.Key] = Normalize(kv.Value?.DeepClone());
                }

                return sorted;
            }

            case JsonArray arr:
            {
                var normalizedItems = arr
                    .Select(item => Normalize(item?.DeepClone()))
                    .ToList();

                // Sort elements by their canonical text for set-style equality.
                normalizedItems.Sort((a, b) =>
                    string.CompareOrdinal(a?.ToJsonString() ?? "null", b?.ToJsonString() ?? "null"));

                var result = new JsonArray();
                foreach (var item in normalizedItems)
                {
                    result.Add(item);
                }

                return result;
            }

            default:
                return node;
        }
    }
}
