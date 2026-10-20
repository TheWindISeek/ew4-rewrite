using System.Text.Json;
using System.Xml.Linq;
using Ew4.Legacy;
using Ew4.Mods;

internal static class CompatibilityAudit
{
    internal static void Run(string root)
    {
        var results = new List<object>();
        var baseNames = ModFiles.Xml(File.ReadAllBytes(Path.Combine(root, "rewrite/data/original/def_areaname.xml"))).Root!
            .Elements().ToDictionary(e => (int)e.Attribute("id")!, e => (string)e.Attribute("name")!);
        foreach (string apk in Directory.EnumerateFiles(root).Where(p => Path.GetExtension(p).Equals(".apk", StringComparison.OrdinalIgnoreCase)).Order())
        {
            using var source = new ImportSource(apk);
            XElement Xml(string name) => ModFiles.Xml(LegacyXml.Normalize(source.Read("assets/" + name), out _)).Root!;
            var cities = Xml("def_areaname.xml").Elements().ToDictionary(e => (int)e.Attribute("id")!, e => (string)e.Attribute("name")!);
            var maps = Xml("def_map.xml").Elements().ToDictionary(e => (int)e.Attribute("id")!,
                e => LegacyTerrainMap.Read(source.Read("assets/" + (string)e.Attribute("file")!)));
            var scenes = new List<object>();
            foreach (string name in source.Names.Where(p => p.StartsWith("assets/", StringComparison.Ordinal) && p.EndsWith(".btl", StringComparison.Ordinal)))
            {
                try
                {
                var battle = LegacyBattle.Read(source.Read(name));
                var map = maps[battle.MapId];
                bool Active(int cell) => cell >= 0 && cell < map.Cells.Count && cell % map.Width >= battle.Left && cell % map.Width < battle.Left + battle.Width
                    && cell / map.Width >= battle.Top && cell / map.Width < battle.Top + battle.Height && map.Cells[cell].BackgroundKind != 255;
                var missing = battle.Sites.Where(s => s.CityId >= 0 && !cities.ContainsKey(s.CityId))
                    .Select(s => new { s.Cell, s.CityId, original = baseNames.GetValueOrDefault(s.CityId) }).ToArray();
                var owners = battle.SectionBytes("ownership").Select((o, i) => (o, i)).Where(p => (sbyte)p.o >= battle.Factions.Count)
                    .Select(p => new { cell = (p.i / battle.Width + battle.Top) * map.Width + p.i % battle.Width + battle.Left, owner = p.o }).ToArray();
                var sites = battle.Sites.Where(s => !Active(s.Cell) || battle.Sites.Count(t => t.Cell == s.Cell) > 1).ToArray();
                var armies = battle.Armies.Where(a => a.Grade < 0 || a.Grade > 2 || a.Unit < 0 || a.Unit > 21 || a.Health > a.MaximumHealth || a.Morale > 1 || a.Morale < -3).ToArray();
                var services = battle.Markets.Select(m => m.Cell).Concat(battle.Taverns.Select(t => t.Cell))
                    .Where(c => !Active(c) || !battle.Sites.Any(s => s.Cell == c && s.BuildingKind != 255)).ToArray();
                var factions = battle.Factions.Where(f => f.State176 > 4).Select(f => new { f.CountryId, f.State176 }).ToArray();
                var negativeFactions = battle.Factions.Where(f => f.Resources.Money < 0 || f.Resources.Industry < 0 || f.Resources.Food < 0).ToArray();
                var taverns = battle.Taverns.Where(t => t.Candidates.Any(c => c.Money < 0 || c.Industry < 0 || c.Medals < 0)).ToArray();
                if (missing.Length + owners.Length + sites.Length + armies.Length + services.Length + factions.Length + negativeFactions.Length + taverns.Length > 0)
                    scenes.Add(new { name, map = new { map.Width, map.Height }, missing, owners, sites, armies, services, factions, negativeFactions, taverns });
                }
                catch (Ew4.Core.RuleViolation e) { scenes.Add(new { name, error = e.Message }); }
            }
            results.Add(new { apk = Path.GetFileName(apk), cities = cities.Count, scenes });
        }
        string output = Path.Combine(root, "rewrite/reports/mod-reference-audit.json");
        File.WriteAllText(output, JsonSerializer.Serialize(results, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("Audited " + results.Count + " APKs: " + output);
    }
}
