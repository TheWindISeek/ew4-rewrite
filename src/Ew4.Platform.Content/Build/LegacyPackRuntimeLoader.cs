using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Ew4.Platform.Engine;

namespace Ew4.Platform.Content.Build;

public sealed record LegacyPackRuntimeLoadOptions(
    bool CorrectOriginalEquipment = true,
    bool VerifySourceHashes = true);

public sealed class LegacyPackRuntimeLoadResult
{
    public required CountryCatalog Countries { get; init; }
    public required ExtensionLoader Extensions { get; init; }
    public required OriginalScenarioCatalogs Catalogs { get; init; }
    public required IReadOnlyDictionary<int, OriginalTerrainMap> Maps { get; init; }
    public required IReadOnlyDictionary<int, string> MapNames { get; init; }
    public required IReadOnlyDictionary<int, string> Cities { get; init; }
    public required IReadOnlyDictionary<string, string> Strings { get; init; }
    public required IReadOnlyDictionary<string, IReadOnlyDictionary<int, string>> CityNamesByScenario { get; init; }
    public required bool CorrectOriginalEquipment { get; init; }
}

/// <summary>
/// 从封存运行时路径（<c>original/</c>、<c>data/</c>）加载 catalog、地图与规则表。
/// </summary>
public static class LegacyModRuntimeLoader
{
    public static LegacyPackRuntimeLoadResult Load(Func<string, byte[]> read, LegacyPackRuntimeLoadOptions? options = null)
    {
        options ??= new LegacyPackRuntimeLoadOptions();
        string Text(string name)
        {
            using var reader = new StreamReader(new MemoryStream(read("original/" + name)), System.Text.Encoding.UTF8, true);
            return reader.ReadToEnd();
        }

        CountryCatalog countries = CountryCatalog.Load(read);
        ExtensionLoader extensions = ExtensionLoader.Load(read, countries);
        string catalogJson = LegacyCatalogSourceTreeOverlay.SyncChangedTables(
            System.Text.Encoding.UTF8.GetString(read("data/catalog.json")),
            name => read("original/" + name));
        catalogJson = LegacyCatalogCommanderOverlay.ApplyIfCommanderSourceChanged(
            catalogJson,
            read("original/def_commander.xml"));
        var runtime = OriginalRuntimeCatalog.FromJson(
            extensions.PatchCatalog(catalogJson),
            options.CorrectOriginalEquipment,
            extensions.LegacySkills,
            extensions.ConfigureItem);
        bool correctOriginalEquipment = options.CorrectOriginalEquipment
            && OriginalEquipmentCorrections.IsOriginal(runtime.SourceHashes["def_item.xml"]);
        extensions.ConfigureRuntime(runtime);
        if (options.VerifySourceHashes)
        {
            runtime.VerifySources(name =>
            {
                byte[] bytes = read("original/" + name);
                return string.Equals(name, "def_commander.xml", StringComparison.Ordinal)
                    ? LegacyTableXmlRepair.NormalizeCommanderTable(bytes)
                    : bytes;
            });
        }

        countries.ValidateRuntime(runtime);
        var buildings = OriginalBuildingCatalog.Read(Text("def_construction.xml"), Text("def_capital.xml"));
        var combat = OriginalCombatCatalog.Read(
            System.Text.Encoding.UTF8.GetString(read("data/runtime_rule_tables.json")),
            Text("def_terraintype.xml"),
            Text("def_installation.xml"),
            buildings,
            extensions.LegacySkills);
        var accounts = OriginalCommanderAccounts.Read(
            runtime,
            combat,
            System.Text.Encoding.UTF8.GetString(read("data/metagame_rule_tables.json")));
        var catalogs = new OriginalScenarioCatalogs(
            runtime,
            OriginalTerrainRules.FromXml(Text("def_mapterrain.xml"), Text("def_terraintype.xml")),
            buildings,
            OriginalRecruitmentCatalog.Read(Text("def_card.xml")),
            combat,
            accounts,
            OriginalItemTradingCatalog.Read(Text("def_item.xml"), options.CorrectOriginalEquipment, runtime.Items),
            OriginalWarzoneTechnology.Read(Text("def_warzonetech.xml")),
            OriginalDialogueCatalog.Read(Text("def_dialogues.xml"), Text("stringtable_cn.ini")));
        var definitions = XDocument.Parse(Text("def_map.xml")).Root!.Elements("map").ToArray();
        var maps = definitions.ToDictionary(
            e => (int)e.Attribute("id")!,
            e => OriginalTerrainMap.Read(read("original/" + (string)e.Attribute("file")!)));
        var mapNames = definitions.ToDictionary(e => (int)e.Attribute("id")!, e => (string)e.Attribute("name")!);
        var cities = XDocument.Parse(Text("def_areaname.xml")).Root!.Elements("areaname")
            .ToDictionary(e => (int)e.Attribute("id")!, e => (string)e.Attribute("name")!);
        var strings = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string line in Text("stringtable_cn.ini").Split('\n'))
        {
            int separator = line.IndexOf('=');
            if (separator > 0 && !line.TrimStart().StartsWith(';'))
            {
                strings[line[..separator].Trim()] = line[(separator + 1)..].Trim();
            }
        }

        return new LegacyPackRuntimeLoadResult
        {
            Countries = countries,
            Extensions = extensions,
            Catalogs = catalogs,
            Maps = maps,
            MapNames = mapNames,
            Cities = cities,
            Strings = strings,
            CityNamesByScenario = BuildScenarioCityNames(Text, cities),
            CorrectOriginalEquipment = correctOriginalEquipment,
        };
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<int, string>> BuildScenarioCityNames(
        Func<string, string> text,
        IReadOnlyDictionary<int, string> cities)
    {
        var battleEntries = XDocument.Parse(text("def_battlelist.xml")).Root!.Elements("battle").ToArray();
        var namesByScenario = new Dictionary<string, IReadOnlyDictionary<int, string>>();
        foreach (var entry in battleEntries.Where(e => e.Attribute("citynames") != null))
        {
            string file = (string)entry.Attribute("citynames")!;
            if (file.Contains('/') || file.Contains('\\') || file.Contains(':') || Path.GetExtension(file) != ".xml")
            {
                throw new RuleViolation("client.city_names", "City name files must be sibling XML resources");
            }

            var names = ScenarioCityNames.Read(text(file), cities);
            namesByScenario.Add(Path.GetFileNameWithoutExtension((string)entry.Attribute("file")!), names);
            if (entry.Attribute("file2") is XAttribute alternate)
            {
                namesByScenario.Add(Path.GetFileNameWithoutExtension(alternate.Value), names);
            }
        }

        return namesByScenario;
    }
}
