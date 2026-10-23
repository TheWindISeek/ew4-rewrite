using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;
using Ew4.Platform.Engine;

namespace Ew4.Platform.Content.Build;

/// <summary>
/// 子包覆盖 legacy 表 XML 时，同步 <c>catalog.json</c> 内对应的 <c>source_trees</c> 与哈希。
/// </summary>
public static class LegacyCatalogSourceTreeOverlay
{
    private static readonly string[] TableSources = ["def_item.xml"];

    public static string SyncChangedTables(string catalogJson, Func<string, byte[]> readOriginalTable)
    {
        var catalog = JsonNode.Parse(catalogJson)!.AsObject();
        if (catalog["source_sha256"] is not JsonObject hashes || catalog["source_trees"] is not JsonObject trees)
        {
            return catalogJson;
        }

        bool changed = false;
        foreach (string file in TableSources)
        {
            if (!hashes.TryGetPropertyValue(file, out JsonNode? hashNode) || hashNode == null)
            {
                continue;
            }

            string? expected = hashNode.GetValue<string>();
            if (string.IsNullOrEmpty(expected))
            {
                continue;
            }

            byte[] bytes = readOriginalTable(file);
            string actual = Convert.ToHexString(SHA256.HashData(bytes));
            if (string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                trees[file] = ImportXmlTree(Encoding.UTF8.GetString(bytes));
            }
            catch (XmlException error)
            {
                throw new RuleViolation(
                    "legacy.catalog_profile",
                    file + " is not well-formed; re-import the Mod APK or fix overlay tables. " + error.Message);
            }
            hashes[file] = actual;
            changed = true;
        }

        return changed ? catalog.ToJsonString() : catalogJson;
    }

    private static JsonObject ImportXmlTree(string xml)
    {
        XElement root = XDocument.Parse(xml).Root
            ?? throw new RuleViolation("legacy.catalog_profile", "Missing XML root");
        return ElementToTree(root);
    }

    private static JsonObject ElementToTree(XElement element)
    {
        var attributes = new JsonObject();
        foreach (XAttribute attribute in element.Attributes())
        {
            attributes[attribute.Name.LocalName] = attribute.Value;
        }

        var children = new JsonArray();
        foreach (XElement child in element.Elements())
        {
            children.Add(ElementToTree(child));
        }

        return new JsonObject
        {
            ["tag"] = element.Name.LocalName,
            ["attributes"] = attributes,
            ["text"] = element.Nodes().OfType<XText>().FirstOrDefault()?.Value ?? "",
            ["children"] = children,
        };
    }
}
