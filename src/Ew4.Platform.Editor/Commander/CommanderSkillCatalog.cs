using System.Globalization;
using System.Text.Json;
using Ew4.Platform.Content;

namespace Ew4.Platform.Editor.Commander;

public sealed record CommanderSkillOption(int LegacyId, string Ew4Id, string DisplayName);

public sealed record CommanderSkillCatalogView(IReadOnlyList<CommanderSkillOption> Skills);

/// <summary>从合并 Mod 的 <c>catalog.json</c> 读取技能表（<c>legacy_id</c> 对应 <c>skill1</c>–<c>skill4</c> 整数）。</summary>
public static class CommanderSkillCatalog
{
    public static CommanderSkillCatalogView Load(IModSource mergedMod)
    {
        if (!TryReadCatalogJson(mergedMod, out byte[]? bytes))
        {
            return new CommanderSkillCatalogView([]);
        }

        using var document = JsonDocument.Parse(bytes);
        if (!document.RootElement.TryGetProperty("skills", out JsonElement skills) || skills.ValueKind != JsonValueKind.Array)
        {
            return new CommanderSkillCatalogView([]);
        }

        var list = new List<CommanderSkillOption>();
        foreach (JsonElement skill in skills.EnumerateArray())
        {
            string? ew4Id = skill.TryGetProperty("id", out JsonElement idEl) ? idEl.GetString() : null;
            if (string.IsNullOrEmpty(ew4Id))
            {
                continue;
            }

            int legacyId = skill.TryGetProperty("legacy_id", out JsonElement legacyEl)
                ? legacyEl.GetInt32()
                : ParseLegacyFromEw4Id(ew4Id);
            string name = ResolveDisplayName(skill);
            list.Add(new CommanderSkillOption(legacyId, ew4Id, name));
        }

        return new CommanderSkillCatalogView(list.OrderBy(s => s.LegacyId).ToList());
    }

    private static bool TryReadCatalogJson(IModSource pack, out byte[]? bytes)
    {
        bytes = null;
        foreach (string path in new[] { "assets/legacy/catalog.json", "assets/client/catalog.json" })
        {
            if (pack.Exists(path))
            {
                bytes = pack.Read(path);
                return true;
            }
        }

        try
        {
            bytes = ModAssetReader.Open(pack)("client/catalog.json");
            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
    }

    private static int ParseLegacyFromEw4Id(string ew4Id)
    {
        const string prefix = "ew4.skill.";
        if (!ew4Id.StartsWith(prefix, StringComparison.Ordinal))
        {
            return -1;
        }

        return int.TryParse(ew4Id[prefix.Length..], NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) ? id : -1;
    }

    private static string ResolveDisplayName(JsonElement skill)
    {
        if (skill.TryGetProperty("names", out JsonElement names))
        {
            if (names.TryGetProperty("zh_CN", out JsonElement zh) && zh.ValueKind == JsonValueKind.String)
            {
                return zh.GetString() ?? "";
            }

            foreach (JsonProperty property in names.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String)
                {
                    return property.Value.GetString() ?? "";
                }
            }
        }

        if (skill.TryGetProperty("legacy_description", out JsonElement legacy) && legacy.ValueKind == JsonValueKind.String)
        {
            string? text = legacy.GetString();
            if (!string.IsNullOrWhiteSpace(text))
            {
                return text.Length > 24 ? text[..24] + "…" : text;
            }
        }

        return skill.TryGetProperty("id", out JsonElement id) ? id.GetString() ?? "" : "";
    }
}
