using System.Xml.Linq;
using Ew4.Platform.Engine;

namespace Ew4.Platform.Content.CatalogLoad;

public static class ScenarioCityNames
{
    public static IReadOnlyDictionary<int, string> Read(string xml, IReadOnlyDictionary<int, string> defaults)
    {
        var root = XDocument.Parse(xml).Root;
        if (root?.Name != "citynames") throw new RuleViolation("client.city_names", "Expected citynames root");
        var names = new Dictionary<int, string>(defaults);
        var seen = new HashSet<int>();
        foreach (var city in root.Elements())
        {
            if (city.Name != "city" || !int.TryParse((string?)city.Attribute("id"), out int id)
                || !defaults.ContainsKey(id) || !seen.Add(id)
                || string.IsNullOrWhiteSpace((string?)city.Attribute("name")))
                throw new RuleViolation("client.city_names", "City names need unique existing IDs and nonempty names");
            names[id] = ((string)city.Attribute("name")!).Trim();
        }
        return names;
    }
}
