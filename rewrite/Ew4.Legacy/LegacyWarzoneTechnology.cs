using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Ew4.Core;

namespace Ew4.Legacy;

public sealed class LegacyWarzoneTechnology
{
    public IReadOnlyDictionary<int, IReadOnlyList<int>> Defaults { get; }
    public string SourceXml { get; }
    public string SourceRevision { get; }
    private LegacyWarzoneTechnology(Dictionary<int, IReadOnlyList<int>> values, string source)
    { Defaults = new ReadOnlyDictionary<int, IReadOnlyList<int>>(values); SourceXml = source;
        SourceRevision = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(source))); }

    public static LegacyWarzoneTechnology Read(string xml)
    {
        var document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        if (document.Root?.Name != "warzones") throw new RuleViolation("legacy.technology", "Missing warzone technology root");
        var values = new Dictionary<int, IReadOnlyList<int>>();
        foreach (var warzone in document.Root.Elements())
        {
            if (warzone.Name != "warzone" || !int.TryParse((string?)warzone.Attribute("id"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) || id < 0)
                throw new RuleViolation("legacy.technology", "Invalid warzone identity");
            var levels = new Dictionary<int, int>();
            foreach (var tech in warzone.Elements())
                if (tech.Name != "tech" || !int.TryParse((string?)tech.Attribute("id"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int index)
                    || !int.TryParse((string?)tech.Attribute("level"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int level)
                    || index < 0 || index >= 26 || level < -1 || !levels.TryAdd(index, level))
                    throw new RuleViolation("legacy.technology", "Invalid or duplicate legacy technology slot");
            if (levels.Count != 26 || !values.TryAdd(id, Array.AsReadOnly(Enumerable.Range(0, 26).Select(i => levels[i]).ToArray())))
                throw new RuleViolation("legacy.technology", "Incomplete technology table or duplicate warzone");
        }
        if (values.Count == 0) throw new RuleViolation("legacy.technology", "Missing warzones");
        return new(values, xml);
    }

    // Native 7dd18 overwrites only the selected player's table in numeric mode 1.
    public LegacyFaction Apply(LegacyFaction scenario, int mode, bool selectedPlayer, int warzone, IReadOnlyList<int>? accountSnapshot = null)
    {
        if (mode < 1 || mode > 4) throw new RuleViolation("legacy.technology_mode", "Unknown legacy game mode");
        if (mode != 1 || !selectedPlayer) return scenario;
        if (!Defaults.TryGetValue(warzone, out var defaults)) throw new RuleViolation("legacy.technology", "Unknown account warzone");
        var levels = accountSnapshot ?? defaults;
        if (levels.Count != 26 || levels.Any(level => level < -1))
            throw new RuleViolation("legacy.technology", "Invalid saved technology snapshot");
        return scenario with { Values68 = Array.AsReadOnly(levels.ToArray()) };
    }
}
