using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;
using Ew4.Platform.Engine;

namespace Ew4.Platform.Content.Build;

/// <summary>
/// 子包覆盖 <c>def_commander.xml</c> 时，父包 <c>catalog.json</c> 中的将领表会过期；按当前 XML 重建 commanders 与 source_trees。
/// </summary>
public static class LegacyCatalogCommanderOverlay
{
    private const string CommanderSource = "def_commander.xml";
    private static readonly string[] CommanderIntegerAttributes =
    [
        "id", "rank", "nobilityrank", "star", "infantry", "cavalry", "artillery", "warship", "fort",
        "business", "movement", "training", "skill1", "skill2", "skill3", "skill4", "item1", "item2", "drawlots", "price",
    ];

    public static string ApplyIfCommanderSourceChanged(string catalogJson, byte[] commanderXmlUtf8)
    {
        commanderXmlUtf8 = LegacyTableXmlRepair.NormalizeCommanderTable(commanderXmlUtf8);
        var catalog = JsonNode.Parse(catalogJson)!.AsObject();
        string expected = catalog["source_sha256"]![CommanderSource]!.GetValue<string>();
        string actual = Convert.ToHexString(SHA256.HashData(commanderXmlUtf8));
        if (string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
        {
            return catalogJson;
        }

        string xml = Encoding.UTF8.GetString(commanderXmlUtf8);
        try
        {
            ApplyCommanderXml(catalog, xml);
        }
        catch (XmlException error)
        {
            throw new RuleViolation(
                "legacy.commander_definition",
                "def_commander.xml is not well-formed; re-import the Mod APK or fix the overlay tables. " + error.Message);
        }

        catalog["source_sha256"]![CommanderSource] = actual;
        return catalog.ToJsonString();
    }

    private static void ApplyCommanderXml(JsonObject catalog, string commanderXml)
    {
        XElement root = XDocument.Parse(commanderXml, LoadOptions.None).Root
            ?? throw new RuleViolation("legacy.commander_definition", "Missing def_commander.xml root");
        if (root.Name.LocalName != "commanders")
        {
            throw new RuleViolation("legacy.commander_definition", "Unexpected def_commander.xml root: " + root.Name.LocalName);
        }

        var children = new JsonArray();
        var commanders = new JsonArray();
        foreach (XElement element in root.Elements("commander"))
        {
            var attributes = new JsonObject();
            foreach (XAttribute attribute in element.Attributes())
            {
                string name = attribute.Name.LocalName;
                attributes[name] = CommanderIntegerAttributes.Contains(name, StringComparer.Ordinal)
                    ? LegacyTableXmlRepair.SanitizeCommanderStatAttribute(attribute.Value, name)
                    : attribute.Value;
            }

            if (!attributes.TryGetPropertyValue("id", out JsonNode? idNode) || idNode == null)
            {
                throw new RuleViolation("legacy.commander_definition", "Commander without id");
            }

            string legacyId = idNode.GetValue<string>();
            children.Add(new JsonObject
            {
                ["tag"] = "commander",
                ["attributes"] = attributes.DeepClone(),
                ["text"] = "",
                ["children"] = new JsonArray(),
            });

            var skills = new JsonArray();
            foreach (string key in new[] { "skill1", "skill2", "skill3", "skill4" })
            {
                if (!attributes.TryGetPropertyValue(key, out JsonNode? skillNode) || skillNode == null)
                {
                    continue;
                }

                if (!int.TryParse(skillNode.GetValue<string>(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int skill)
                    || skill < 0)
                {
                    continue;
                }

                skills.Add("ew4.skill." + skill.ToString(CultureInfo.InvariantCulture));
            }

            commanders.Add(new JsonObject
            {
                ["id"] = "ew4.commander." + legacyId,
                ["legacy_attributes"] = attributes.DeepClone(),
                ["skills"] = skills,
            });
        }

        catalog["commanders"] = commanders;
        catalog["source_trees"]![CommanderSource] = new JsonObject
        {
            ["tag"] = "commanders",
            ["attributes"] = new JsonObject(),
            ["text"] = "",
            ["children"] = children,
        };
        EnsureSkillIds(catalog, commanders);
    }

    private static void EnsureSkillIds(JsonObject catalog, JsonArray commanders)
    {
        JsonArray skills = catalog["skills"]?.AsArray() ?? throw new RuleViolation("legacy.catalog_reference", "Missing catalog skills");
        var known = skills.Select(node => node?["id"]?.GetValue<string>())
            .Where(id => !string.IsNullOrEmpty(id))
            .ToHashSet(StringComparer.Ordinal);
        foreach (JsonNode? commander in commanders)
        {
            if (commander?["skills"] is not JsonArray commanderSkills)
            {
                continue;
            }

            foreach (JsonNode? skill in commanderSkills)
            {
                string? id = skill?.GetValue<string>();
                if (string.IsNullOrEmpty(id) || known.Contains(id))
                {
                    continue;
                }

                skills.Add(new JsonObject { ["id"] = id });
                known.Add(id);
            }
        }
    }
}
