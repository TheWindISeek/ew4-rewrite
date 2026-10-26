using System.Text.Json;
using Ew4.Interface.Editor;

namespace Ew4.Service.Host;

internal static class EditorRuntimeJson
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static string CommanderTable(CommanderTableView view) => JsonSerializer.Serialize(view, Web);

    public static string CommanderEntry(CommanderEntry entry) => JsonSerializer.Serialize(entry, Web);

    public static string SkillCatalog(CommanderSkillCatalogView view) => JsonSerializer.Serialize(view, Web);

    public static string CommanderEditOutcome(CommanderEditOutcome outcome) => JsonSerializer.Serialize(outcome, Web);

    public static CommanderEditLoadout ParseCommanderEdit(int legacyId, byte[] utf8)
    {
        using var document = JsonDocument.Parse(utf8);
        JsonElement root = document.RootElement;
        return new CommanderEditLoadout(
            legacyId,
            root.TryGetProperty("skill1", out JsonElement s1) ? s1.GetInt32() : 0,
            root.TryGetProperty("skill2", out JsonElement s2) ? s2.GetInt32() : 0,
            root.TryGetProperty("skill3", out JsonElement s3) ? s3.GetInt32() : 0,
            root.TryGetProperty("skill4", out JsonElement s4) ? s4.GetInt32() : 0,
            root.TryGetProperty("infantry", out JsonElement i) ? i.GetInt32() : 0,
            root.TryGetProperty("cavalry", out JsonElement c) ? c.GetInt32() : 0,
            root.TryGetProperty("artillery", out JsonElement a) ? a.GetInt32() : 0,
            root.TryGetProperty("warship", out JsonElement w) ? w.GetInt32() : 0,
            root.TryGetProperty("fort", out JsonElement f) ? f.GetInt32() : 0);
    }
}
