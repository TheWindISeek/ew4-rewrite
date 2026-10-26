using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Ew4.Platform.Content.CatalogLoad;

/// <summary>
/// APK 覆盖表常见笔误的轻量修复（导入与运行时 catalog 重建共用）。
/// </summary>
public static partial class LegacyTableXmlRepair
{
    private static readonly string[] CommanderIntegerAttributes =
    [
        "id", "rank", "nobilityrank", "star", "infantry", "cavalry", "artillery", "warship", "fort",
        "business", "movement", "training", "skill1", "skill2", "skill3", "skill4", "item1", "item2", "drawlots", "price",
    ];

    private static readonly HashSet<string> CommanderNonNegativeAttributes = new(StringComparer.Ordinal)
    {
        "infantry", "cavalry", "artillery", "warship", "fort", "business", "movement", "training", "drawlots", "price",
    };

    public static byte[] NormalizeCommanderTable(byte[] utf8)
    {
        string text = Encoding.UTF8.GetString(utf8);
        text = MissingAttributeSpaceRegex().Replace(text, "\"$1\" $2=");
        if (!TryParse(text, out XDocument? document) || document?.Root == null)
        {
            return Encoding.UTF8.GetBytes(text);
        }

        foreach (XElement commander in document.Root!.Elements("commander"))
        {
            foreach (string key in CommanderIntegerAttributes)
            {
                XAttribute? attribute = commander.Attribute(key);
                if (attribute == null)
                {
                    continue;
                }

                if (!int.TryParse(attribute.Value, out int parsed))
                {
                    attribute.Value = "0";
                    continue;
                }

                if (CommanderNonNegativeAttributes.Contains(key) && parsed < 0)
                {
                    attribute.Value = "0";
                }
            }
        }

        return Encoding.UTF8.GetBytes(document.ToString(SaveOptions.DisableFormatting));
    }

    public static string SanitizeIntegerAttribute(string? value, int fallback = 0)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        if (!int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out int parsed))
        {
            return fallback.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return parsed.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    public static string SanitizeCommanderStatAttribute(string? value, string attributeName, int fallback = 0)
    {
        string sanitized = SanitizeIntegerAttribute(value, fallback);
        if (!CommanderNonNegativeAttributes.Contains(attributeName))
        {
            return sanitized;
        }

        int parsed = int.Parse(sanitized, System.Globalization.CultureInfo.InvariantCulture);
        return Math.Max(0, parsed).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static bool TryParse(string text, out XDocument? document)
    {
        try
        {
            document = XDocument.Parse(text, LoadOptions.None);
            return document.Root != null;
        }
        catch (XmlException)
        {
            document = null;
            return false;
        }
    }

    [GeneratedRegex(@"""(\d+)""([a-zA-Z_]+)=", RegexOptions.CultureInvariant)]
    private static partial Regex MissingAttributeSpaceRegex();
}
