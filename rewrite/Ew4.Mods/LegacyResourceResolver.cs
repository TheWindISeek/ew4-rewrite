using System.Text;
using System.Xml.Linq;
using Ew4.Legacy;

namespace Ew4.Mods;

internal sealed record LegacyResourceResolution(string Resource, string Source, string SourceHash, string ResolvedHash, string Reason);

internal sealed class LegacyResourceResolver
{
    private readonly ModBase baseline;
    private readonly ImportSource source;
    private readonly string prefix;
    private readonly List<string> warnings;
    private readonly Dictionary<string, string> hashes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, byte[]> xml = new(StringComparer.Ordinal);
    private readonly Dictionary<string, LegacyResourceResolution> resolutions = new(StringComparer.Ordinal);
    internal HashSet<string> Names { get; }
    internal bool FullPackage { get; }
    internal IReadOnlyCollection<LegacyResourceResolution> Resolutions => resolutions.Values;

    internal LegacyResourceResolver(ModBase baseline, ImportSource source, List<string> warnings)
    {
        this.baseline = baseline; this.source = source; this.warnings = warnings;
        prefix = source.Names.Any(n => n.StartsWith("assets/", StringComparison.Ordinal)) ? "assets/" : "";
        Names = source.Names.Where(n => n.StartsWith(prefix, StringComparison.Ordinal)).Select(n => n[prefix.Length..]).ToHashSet(StringComparer.Ordinal);
        FullPackage = source.Contains("AndroidManifest.xml");
        try { foreach (var p in ModFiles.Object(baseline.Read("client/import-baseline.json"))) hashes[p.Key] = p.Value!.GetValue<string>(); }
        catch (FileNotFoundException) { }
    }

    internal bool Changed(string name) => Names.Contains(name)
        && (!hashes.TryGetValue(name, out string? hash) || ModFiles.Hash(source.Read(prefix + name)) != hash);

    internal byte[] Read(string name)
    {
        ModFiles.PathName(name);
        if (xml.TryGetValue(name, out var cached)) return cached;
        bool present = Names.Contains(name);
        if (!present && FullPackage && (name.EndsWith(".btl", StringComparison.Ordinal) || name.EndsWith(".bin", StringComparison.Ordinal)))
            throw new InvalidDataException("完整APK缺少引用的剧本或地图，不能替换成原版: " + name);
        byte[] bytes;
        try { bytes = present ? source.Read(prefix + name) : baseline.Read("original/" + name); }
        catch (Exception e) when (e is IOException or KeyNotFoundException)
        { throw new InvalidDataException("资源及内置回退均不存在: " + name, e); }
        string sourceHash = ModFiles.Hash(bytes);
        string reason = present ? "package" : "missing-file-fallback";
        if (name.EndsWith(".xml", StringComparison.Ordinal))
        {
            try
            {
                bytes = LegacyXml.Normalize(bytes, out bool changed);
                if (changed) { reason = "legacy-xml-normalized"; warnings.Add("已按原读取方式规范化XML: " + name); }
            }
            catch (Exception e) when (e is InvalidDataException or System.Xml.XmlException)
            { throw new InvalidDataException(name + ": " + e.Message, e); }
            xml[name] = bytes;
        }
        if (!present && !resolutions.ContainsKey(name)) warnings.Add("缺少共享资源，使用内置原版: " + name);
        resolutions[name] = new(name, present ? "package:" + prefix + name : "builtin:original/" + name,
            sourceHash, ModFiles.Hash(bytes), reason);
        return bytes;
    }

    internal void CompleteCityLabels()
    {
        if (!Names.Contains("def_areaname.xml")) return;
        var labels = ModFiles.Xml(Read("def_areaname.xml"));
        var known = labels.Root!.Elements("areaname").Select(e => (int)e.Attribute("id")!).ToHashSet();
        var originalLabels = ModFiles.Xml(baseline.Read("original/def_areaname.xml")).Root!.Elements("areaname")
            .ToDictionary(e => (int)e.Attribute("id")!);
        if (originalLabels.Keys.All(known.Contains)) return;
        var maps = ModFiles.Xml(Read("def_map.xml")).Root!.Elements("map")
            .ToDictionary(e => (int)e.Attribute("id")!, e => (string)e.Attribute("file")!);
        var originalMaps = ModFiles.Xml(baseline.Read("original/def_map.xml")).Root!.Elements("map")
            .ToDictionary(e => (int)e.Attribute("id")!, e => (string)e.Attribute("file")!);
        var sameMaps = new Dictionary<int, bool>();
        int added = 0;
        foreach (string scenario in Names.Where(n => !n.Contains('/') && n.EndsWith(".btl", StringComparison.Ordinal)).Order(StringComparer.Ordinal))
        {
            if (!baseline.Files.Contains("original/" + scenario)) continue;
            LegacyBattle battle;
            try { battle = LegacyBattle.Read(Read(scenario)); }
            catch (Ew4.Core.RuleViolation) { continue; } // Scene validation reports malformed BTL separately; label completion is optional.
            if (!battle.Sites.Any(s => s.CityId >= 0 && !known.Contains(s.CityId) && originalLabels.ContainsKey(s.CityId))) continue;
            if (!sameMaps.TryGetValue(battle.MapId, out bool same))
            {
                same = maps.TryGetValue(battle.MapId, out string? map) && originalMaps.TryGetValue(battle.MapId, out string? original)
                    && Read(map).AsSpan().SequenceEqual(baseline.Read("original/" + original));
                sameMaps[battle.MapId] = same;
            }
            if (!same) continue;
            var originalBattle = LegacyBattle.Read(baseline.Read("original/" + scenario));
            if (originalBattle.MapId != battle.MapId) continue;
            var originalSites = originalBattle.Sites.GroupBy(s => s.Cell).ToDictionary(g => g.Key, g => g.Last());
            foreach (var site in battle.Sites)
            {
                if (known.Contains(site.CityId) || !originalLabels.TryGetValue(site.CityId, out var label)
                    || !originalSites.TryGetValue(site.Cell, out var originalSite) || originalSite.CityId != site.CityId || originalSite.BuildingKind != site.BuildingKind) continue;
                labels.Root.Add(new XElement(label)); known.Add(site.CityId); added++;
                string resource = "def_areaname.xml#" + site.CityId;
                string hash = ModFiles.Hash(Encoding.UTF8.GetBytes(label.ToString(SaveOptions.DisableFormatting)));
                resolutions[resource] = new(resource, "builtin:original/" + resource, hash, hash, "matching-map-site-id");
            }
        }
        if (added == 0) return;
        byte[] resolved = Encoding.UTF8.GetBytes(labels.ToString(SaveOptions.DisableFormatting));
        xml["def_areaname.xml"] = resolved;
        resolutions["def_areaname.xml"] = resolutions["def_areaname.xml"] with { ResolvedHash = ModFiles.Hash(resolved), Reason = "city-labels-completed" };
        warnings.Add("依据相同底图、剧本位置及地点ID补齐原版城市名称: " + added);
    }

    internal void CheckMapReferences()
    {
        foreach (var map in ModFiles.Xml(Read("def_map.xml")).Root!.Elements("map"))
            Read((string)map.Attribute("file")!);
    }
}
