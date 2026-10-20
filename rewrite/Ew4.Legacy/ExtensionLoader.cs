using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Ew4.Core;

namespace Ew4.Legacy;

public sealed record ExtensionLayout(string Mode = "list", int Columns = 1, int Gap = 4, int IconSize = 19, int MaxHeight = 130);
public sealed record ExtensionBinding(string Target, string Mode, SkillSlot[]? Skills = null, string[]? Traits = null);
public sealed class ExtensionLoader
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    public RuleEventBus Bus { get; }
    public string Revision { get; }
    public IReadOnlyList<ExtensionBinding> UnitBindings { get; }
    public IReadOnlyList<ExtensionBinding> CommanderBindings { get; }
    public IReadOnlyDictionary<string, ExtensionVictoryRules> Victories { get; }
    public IReadOnlyDictionary<string, ExtensionLayout> Layouts { get; }
    public bool Enabled => Revision.Length > 0;
    private ExtensionLoader(RuleEventBus bus, string revision, ExtensionBinding[] units, ExtensionBinding[] commanders,
        Dictionary<string, ExtensionVictoryRules> victories, Dictionary<string, ExtensionLayout> layouts)
    { Bus = bus; Revision = revision; UnitBindings = units; CommanderBindings = commanders; Victories = victories; Layouts = layouts; }
    private sealed record Manifest(int Schema, int ApiVersion, string[]? Skills = null, string[]? Traits = null,
        string? UnitBindings = null, string? CommanderBindings = null, string? VictoryRules = null, string? Presentation = null, string[]? Scripts = null);

    public static bool PathValid(string p) => p.StartsWith("rewrite/", StringComparison.Ordinal) && p.Length < 240
        && !p.Contains('\\') && !p.Contains(':') && p.Split('/').All(s => s.Length > 0 && s != "." && s != "..")
        && p.All(c => c >= 32);
    public static ExtensionLoader Load(Func<string, byte[]> read)
    {
        byte[] bytes;
        try { bytes = read("rewrite/extension.json"); }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException or KeyNotFoundException)
        { return new(new(new Dictionary<string, ExtensionSkill>()), "", [], [], new(), new()); }
        var loaded = new SortedDictionary<string, byte[]>(StringComparer.Ordinal);
        T Read<T>(string path)
        {
            if (!PathValid(path) || !path.EndsWith(".json", StringComparison.Ordinal)) throw new RuleViolation("extension.path", path);
            var data = path == "rewrite/extension.json" ? bytes : read(path);
            if (data.Length > 2 * 1024 * 1024) throw new RuleViolation("extension.size", path);
            loaded[path] = data;
            using var document = JsonDocument.Parse(data, new JsonDocumentOptions { MaxDepth = 32 });
            Unique(document.RootElement);
            return JsonSerializer.Deserialize<T>(data, Json) ?? throw new RuleViolation("extension.json", path);
        }
        var m = Read<Manifest>("rewrite/extension.json");
        if (m.Schema != 1 || m.ApiVersion != 1) throw new RuleViolation("extension.version", "Unsupported extension schema/API version");
        var skills = new Dictionary<string, ExtensionSkill>(StringComparer.Ordinal);
        foreach (string path in (m.Skills ?? []).Concat(m.Traits ?? []))
            foreach (var skill in Read<ExtensionSkill[]>(path))
            {
                if (!Id(skill.Id) || skill.Kind is not ("passive" or "active" or "tag") || skill.Target is not ("self" or "ally" or "enemy")
                    || skill.Range < 0 || skill.Cooldown < 0 || !skills.TryAdd(skill.Id, skill)
                    || skill.Icon != null && (!PathValid(skill.Icon) || !skill.Icon.EndsWith(".png", StringComparison.Ordinal)))
                    throw new RuleViolation("extension.skill", "Invalid/duplicate skill: " + skill.Id);
                skill.Cost.Validate();
                if (skill.Cost.Money < 0 || skill.Cost.Industry < 0 || skill.Cost.Food < 0) throw new RuleViolation("extension.cost", skill.Id);
                foreach (var e in skill.Effects) ValidateEffect(e);
            }
        var units = m.UnitBindings == null ? [] : Read<ExtensionBinding[]>(m.UnitBindings);
        var commanders = m.CommanderBindings == null ? [] : Read<ExtensionBinding[]>(m.CommanderBindings);
        foreach (var binding in units.Concat(commanders))
        {
            if (string.IsNullOrWhiteSpace(binding.Target) || binding.Mode is not ("append" or "replace" or "remove"))
                throw new RuleViolation("extension.binding", "Invalid binding target/mode");
            var slots = binding.Skills ?? [];
            if (slots.Any(s => !Known(s.Id) || s.Level < 1 || string.IsNullOrWhiteSpace(s.InstanceId) || s.InstanceId.Contains('/')
                || s.InstanceId.Length > 64 || s.X.HasValue != s.Y.HasValue || !float.IsFinite(s.X ?? 0) || !float.IsFinite(s.Y ?? 0))
                || slots.Select(s => s.InstanceId).Distinct().Count() != slots.Length || (binding.Traits ?? []).Any(t => !Known(t)))
                throw new RuleViolation("extension.binding", "Invalid skill reference/instance/position: " + binding.Target);
        }
        var victories = m.VictoryRules == null ? new Dictionary<string, ExtensionVictoryRules>() : Read<Dictionary<string, ExtensionVictoryRules>>(m.VictoryRules);
        foreach (var v in victories.Values) ValidateVictory(v);
        var layouts = m.Presentation == null ? new Dictionary<string, ExtensionLayout>() : Read<Dictionary<string, ExtensionLayout>>(m.Presentation);
        foreach (var layout in layouts)
            if (layout.Key is not ("commander" or "unit") || layout.Value.Mode is not ("list" or "grid" or "positioned")
                || layout.Value.Columns is < 1 or > 16 || layout.Value.Gap is < 0 or > 32 || layout.Value.IconSize is < 12 or > 64
                || layout.Value.MaxHeight is < 40 or > 400) throw new RuleViolation("extension.layout", layout.Key);
        var scripts = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string path in skills.Values.SelectMany(s => s.Effects).Select(e => e.Script)
            .Concat(victories.Values.SelectMany(v => Conditions(v.Win).Concat(v.Lose == null ? [] : Conditions(v.Lose))).Select(c => c.Script))
            .Where(s => s != null).Select(s => s!).Concat(m.Scripts ?? []).Distinct())
        {
            if (!PathValid(path) || !path.EndsWith(".lua", StringComparison.Ordinal)) throw new RuleViolation("extension.path", path);
            var data = read(path);
            if (data.Length > 256 * 1024) throw new RuleViolation("extension.size", path);
            loaded[path] = data; scripts[path] = Encoding.UTF8.GetString(data);
        }
        var runtime = new ExtensionScriptRuntime(scripts);
        foreach (var skill in skills.Values) if (skill.Icon != null)
        {
            var data = read(skill.Icon);
            if (data.Length > 4 * 1024 * 1024) throw new RuleViolation("extension.size", skill.Icon);
        }
        string revision = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', loaded.Select(p => p.Key + ":" + Convert.ToHexStringLower(SHA256.HashData(p.Value)))))));
        return new(new(skills, runtime), revision, units, commanders, victories, layouts);
        bool Known(string id) => skills.ContainsKey(id) || id.StartsWith("ew4.skill.", StringComparison.Ordinal)
            && int.TryParse(id[10..], out int value) && value is >= 0 and < 40 || BuiltinTraits.Contains(id);
    }
    public static readonly string[] BuiltinTraits = ["no_retaliation", "line_splash", "siege_gun", "coastal_gun", "consumed_on_attack",
        "ai.rocket", "ai.direct_artillery", "ai.counter_mounted"];
    private static bool Id(string id) => id.Length is > 0 and <= 128 && id.Contains('.') && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-');
    private static void Unique(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in node.EnumerateObject()) { if (!names.Add(p.Name)) throw new RuleViolation("extension.json", "Duplicate JSON key: " + p.Name); Unique(p.Value); }
        }
        else if (node.ValueKind == JsonValueKind.Array) foreach (var v in node.EnumerateArray()) Unique(v);
    }
    private static void ValidateEffect(ExtensionEffect e)
    {
        bool valid = e.Event switch {
            "combat.modify_damage" => e.Operation is "damage_multiplier" or "damage_add",
            "combat.counterattack" => e.Operation == "counterattack",
            "unit.recruited" or "unit.move_end" or "unit.captured" or "combat.after" or "turn.start" or "turn.end" or "skill.activate" or "scenario.start"
                => e.Operation is "heal" or "money" or "industry" or "food",
            _ => false };
        if (!valid || !double.IsFinite(e.Value) || e.Value < 0 || e.Script == null != (e.Function == null)
            || e.Function != null && !System.Text.RegularExpressions.Regex.IsMatch(e.Function, "^[A-Za-z_][A-Za-z0-9_]*$"))
            throw new RuleViolation("extension.effect", "Invalid event/operation/handler: " + e.Event + "/" + e.Operation);
    }
    private static IEnumerable<VictoryCondition> Conditions(VictoryCondition c) => new[] { c }.Concat(c.Conditions.SelectMany(Conditions));
    public static void ValidateVictory(ExtensionVictoryRules v)
    {
        if (v.Priority is not ("victory" or "defeat")) throw new RuleViolation("extension.victory", "Invalid outcome priority");
        foreach (var c in Conditions(v.Win).Concat(v.Lose == null ? [] : Conditions(v.Lose)))
        {
            if (c.Type is not ("all" or "any" or "enemy_factions_defeated" or "factions_defeated" or "units_destroyed" or "units_lost"
                or "cells_owned" or "cells_lost" or "enemy_objectives_cleared" or "round_reached" or "resources" or "script")
                || c.Type is "all" or "any" && c.Conditions.Count == 0 || c.Round < 1
                || c.Type is "cells_owned" or "cells_lost" && c.Cells.Count == 0
                || c.Type is "units_destroyed" or "units_lost" && c.Units.Count == 0
                || c.Type == "factions_defeated" && c.Factions.Count == 0
                || c.Type == "script" && (c.Script == null || string.IsNullOrWhiteSpace(c.Function)))
                throw new RuleViolation("extension.victory", "Invalid condition: " + c.Type);
            c.Resources.Validate();
        }
    }

    public string PatchCatalog(string json)
    {
        if (!Enabled) return json;
        var catalog = JsonNode.Parse(json)!.AsObject();
        catalog["overridden_skills"] = JsonSerializer.SerializeToNode(Bus.Skills.Keys.Where(s => s.StartsWith("ew4.skill.", StringComparison.Ordinal)).ToArray());
        foreach (var definition in Bus.Skills.Values)
            if (!catalog["skills"]!.AsArray().Any(s => (string?)s!["id"] == definition.Id))
                catalog["skills"]!.AsArray().Add(new JsonObject { ["id"] = definition.Id });
        Apply("profiles", UnitBindings, true); Apply("commanders", CommanderBindings, false);
        return catalog.ToJsonString();
        void Apply(string collection, IReadOnlyList<ExtensionBinding> bindings, bool unit)
        {
            foreach (var binding in bindings)
            {
                var entry = catalog[collection]!.AsArray().SingleOrDefault(e => (string?)e!["id"] == binding.Target)
                    ?? throw new RuleViolation("extension.binding_target", binding.Target);
                var oldSlots = entry["skill_slots"]?.Deserialize<SkillSlot[]>(Json)
                    ?? (unit ? [] : entry["skills"]!.AsArray().Select((s, i) => new SkillSlot((string)s!, "legacy." + i)).ToArray());
                var slots = Merge(oldSlots, binding.Skills ?? [], s => s.InstanceId, binding.Mode);
                entry["skill_slots"] = JsonSerializer.SerializeToNode(slots, Json);
                if (!unit) entry["skills"] = JsonSerializer.SerializeToNode(slots.Select(s => s.Id).ToArray());
                if (unit && binding.Traits != null) entry["traits"] = JsonSerializer.SerializeToNode(Merge(
                    entry["traits"]!.AsArray().Select(s => (string)s!).ToArray(), binding.Traits, s => s, binding.Mode));
            }
        }
    }
    private static T[] Merge<T>(T[] old, T[] values, Func<T, string> key, string mode)
    {
        if (mode == "replace") return values;
        var names = values.Select(key).ToHashSet(StringComparer.Ordinal);
        if (mode == "append" && old.Any(v => names.Contains(key(v)))) throw new RuleViolation("extension.instance", "Duplicate skill instance");
        return mode == "remove" ? old.Where(v => !names.Contains(key(v))).ToArray() : old.Concat(values).ToArray();
    }
}
