using System.Text.Json;
using System.Text.Json.Serialization;
using Ew4.Platform.Engine;

namespace Ew4.Platform.Content.CatalogLoad;

/// <summary>Mod 包内 JSON 解析（BTL 扩展块、catalog 等），与规则扩展无关。</summary>
public static class ModContentJson
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    public static T ParseJson<T>(byte[] data, bool caseSensitiveKeys = false)
    {
        if (data.Length > 2 * 1024 * 1024)
        {
            throw new RuleViolation("content.json", "JSON exceeds 2 MiB");
        }

        using var document = JsonDocument.Parse(data, new JsonDocumentOptions { MaxDepth = 32 });
        Unique(document.RootElement, caseSensitiveKeys);
        return JsonSerializer.Deserialize<T>(data, caseSensitiveKeys
            ? new JsonSerializerOptions(Json) { PropertyNameCaseInsensitive = false }
            : Json) ?? throw new RuleViolation("content.json", "Missing JSON value");
    }

    private static void Unique(JsonElement node, bool caseSensitiveKeys = false)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(caseSensitiveKeys ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase);
            foreach (var property in node.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new RuleViolation("content.json", "Duplicate JSON key: " + property.Name);
                }

                Unique(property.Value, caseSensitiveKeys);
            }
        }
        else if (node.ValueKind == JsonValueKind.Array)
        {
            foreach (var value in node.EnumerateArray())
            {
                Unique(value, caseSensitiveKeys);
            }
        }
    }
}
