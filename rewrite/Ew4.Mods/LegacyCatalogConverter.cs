using System.Globalization;
using System.Text.Json.Nodes;
using System.Xml.Linq;

namespace Ew4.Mods;

internal static class LegacyCatalogConverter
{
    internal static readonly string[] Sources = ["def_army.xml", "def_commander.xml", "def_item.xml", "def_card.xml",
        "def_construction.xml", "def_installation.xml", "def_terraintype.xml", "def_conquestlist.xml", "stringtable_cn.ini"];
    internal static string Slug(string name) => System.Text.RegularExpressions.Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9]+", "_").Trim('_');
    internal static JsonObject Attr(XElement e) => new(e.Attributes().Select(a => KeyValuePair.Create<string, JsonNode?>(a.Name.LocalName, JsonValue.Create(a.Value))));
    internal static JsonObject Tree(XElement e) => new() { ["tag"] = e.Name.LocalName, ["attributes"] = Attr(e),
        ["text"] = string.Concat(e.Nodes().OfType<XText>().Select(t => t.Value)).Trim(), ["children"] = new JsonArray(e.Elements().Select(x => (JsonNode)Tree(x)).ToArray()) };
    internal static JsonObject Layout(XElement e) => new() { ["attributes"] = Attr(e), ["children"] = new JsonArray(e.Elements().Select(x => (JsonNode)Layout(x)).ToArray()) };
    internal static Dictionary<string, string> Strings(byte[] bytes) => ModFiles.Text(bytes).Split('\n')
        .Where(l => l.Contains('=') && !l.TrimStart().StartsWith(';')).Select(l => l.Split('=', 2))
        .GroupBy(p => p[0].Trim()).ToDictionary(g => g.Key, g => g.Last()[1].Trim());
    internal static JsonObject Convert(Func<string, byte[]> read, JsonObject baseline)
    {
        var roots = Sources.Where(s => s.EndsWith(".xml", StringComparison.Ordinal)).ToDictionary(s => s, s => ModFiles.Xml(read(s)).Root!);
        var strings = Strings(read("stringtable_cn.ini"));
        var tables = baseline["native_tables"]!.DeepClone();
        var profiles = new JsonArray(); var variants = new Dictionary<string, (string? Parent, JsonObject Profiles)>();
        string[] weapons = ["gun", "guns", "mgun", "cannon", "rocket", "cold"], categories = ["infantry", "cavalry", "artillery", "naval", "fort"];
        foreach (var country in roots["def_army.xml"].Elements())
        {
            string variant = (string)country.Attribute("name")!;
            var entries = new JsonObject(); variants.Add(variant, ((string?)country.Attribute("ref"), entries));
            foreach (var army in country.Elements("army"))
            {
                string A(string key) => (string?)army.Attribute(key) ?? throw new InvalidDataException("兵种缺少属性: " + key);
                int N(string key) => int.Parse(A(key), CultureInfo.InvariantCulture);
                int grade = N("grade"), weapon = Array.IndexOf(weapons, A("weapon")); string name = A("name"), slug = Slug(name);
                if (grade is < 0 or > 2 || weapon < 0) throw new InvalidDataException("原格式仅支持0至2级和已实现的武器类型: " + name);
                string category = A("type") == "warship" ? "naval" : A("type");
                if (!categories.Contains(category)) throw new InvalidDataException("未实现的兵种分类: " + category);
                string id = $"ew4.unit.{variant}.{slug}.{grade}"; entries.Add($"{slug}.{grade}", id);
                string[] traits = name switch { "Rocket" => ["no_retaliation", "line_splash", "ai.rocket"],
                    "Light Artillery" or "Heavy Artillery" => ["ai.direct_artillery"], "Siege Artillery" => ["siege_gun", "ai.direct_artillery"],
                    "Coastal Fort" => ["coastal_gun"], "Machine Gun" or "Armored Car" => ["ai.counter_mounted"], _ => [] };
                int strength = N("strength");
                // This Mod sentinel is a single-use weapon, not negative living health.
                if (strength == -999) traits = [.. traits, "consumed_on_attack"];
                var multipliers = new JsonObject();
                for (int i = 0; i < 5; i++) multipliers[categories[i]] = tables["weapon_category_multipliers"]![weapon * 5 + i]!.DeepClone();
                var bands = new JsonArray();
                for (int i = 0; i < grade + 4; i++) bands.Add(new JsonObject { ["strict_minimum_percent"] = tables["attack_count_health_thresholds"]![i]!.DeepClone(), ["rolls"] = i + 2 });
                profiles.Add(new JsonObject { ["id"] = id, ["category"] = category, ["grade"] = grade.ToString(CultureInfo.InvariantCulture),
                    ["max_health"] = strength == -999 ? 1 : strength, ["movement"] = N("movement"), ["minimum_attack"] = N("minatk"), ["maximum_attack"] = N("maxatk"),
                    ["minimum_range"] = N("minatkrange"), ["maximum_range"] = N("maxatkrange"), ["food_consumption"] = N("consumption"),
                    ["traits"] = new JsonArray(traits.Select(t => (JsonNode)JsonValue.Create(t)!).ToArray()), ["kill_experience"] = grade + 1,
                    ["ignition_numerator"] = weapon == 3 ? 1 : weapon == 4 ? 5 : 0, ["ignition_denominator"] = weapon == 4 ? 2 : 1,
                    ["attack_bands"] = bands, ["damage_multipliers"] = multipliers });
            }
        }
        var resolved = new JsonObject();
        JsonObject Resolve(string id, HashSet<string> visiting)
        {
            if (resolved[id] is JsonObject found) return found;
            if (!visiting.Add(id) || !variants.TryGetValue(id, out var entry)) throw new InvalidDataException("兵种国家继承无效: " + id);
            var result = entry.Parent == null ? new JsonObject() : (JsonObject)Resolve(entry.Parent, visiting).DeepClone();
            foreach (var pair in entry.Profiles) result[pair.Key] = pair.Value!.DeepClone();
            visiting.Remove(id); resolved[id] = result; return result;
        }
        foreach (string id in variants.Keys) Resolve(id, []);
        var skills = new JsonArray();
        for (int i = 0; i < 40; i++) skills.Add(new JsonObject { ["id"] = "ew4.skill." + i, ["legacy_id"] = i,
            ["names"] = new JsonObject { ["zh_CN"] = strings.GetValueOrDefault($"name_skill_{i + 1:00}", $"技能{i}") },
            ["legacy_description"] = strings.GetValueOrDefault($"desc_skill_{i + 1:00}", "") });
        var commanders = new JsonArray();
        foreach (var e in roots["def_commander.xml"].Elements())
        {
            var list = new JsonArray();
            for (int i = 1; i <= 4; i++)
            {
                int skill = (int?)e.Attribute("skill" + i) ?? -1;
                if (skill >= 40) throw new InvalidDataException("尚未实现的原生技能ID: " + skill);
                if (skill >= 0) list.Add("ew4.skill." + skill);
            }
            commanders.Add(new JsonObject { ["id"] = "ew4.commander." + (int)e.Attribute("id")!, ["legacy_attributes"] = Attr(e), ["skills"] = list });
        }
        return new JsonObject { ["schema_version"] = 2, ["origin"] = "Offline resource import; native executable code excluded",
            ["source_sha256"] = new JsonObject(Sources.Select(s => KeyValuePair.Create<string, JsonNode?>(s, JsonValue.Create(ModFiles.Hash(read(s)))))),
            ["profiles"] = profiles, ["variants"] = resolved, ["skills"] = skills, ["commanders"] = commanders,
            ["source_trees"] = new JsonObject(roots.Select(p => KeyValuePair.Create<string, JsonNode?>(p.Key, Tree(p.Value)))), ["native_tables"] = tables };
    }
}
