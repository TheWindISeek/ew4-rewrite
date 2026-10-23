using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Ew4.Platform.Engine;

namespace Ew4.Platform.Content.Build;

/// <summary>
/// 从已激活 Pack 加载原版运行时目录（catalog、地图、规则表），供剧本开局使用。
/// </summary>
public sealed class PackRuntimeBootstrap
{
    private readonly Func<string, byte[]> _read;

    public PackRuntimeBootstrap(IPackSource pack)
    {
        _read = PackAssetRead.FromPack(pack);
        string Text(string name)
        {
            using var reader = new StreamReader(new MemoryStream(_read("original/" + name)), System.Text.Encoding.UTF8, true);
            return reader.ReadToEnd();
        }

        Countries = CountryCatalog.Load(_read);
        Extensions = ExtensionLoader.Load(_read, Countries);
        string catalogJson = LegacyCatalogSourceTreeOverlay.SyncChangedTables(
            System.Text.Encoding.UTF8.GetString(_read("data/catalog.json")),
            name => _read("original/" + name));
        catalogJson = LegacyCatalogCommanderOverlay.ApplyIfCommanderSourceChanged(
            catalogJson,
            _read("original/def_commander.xml"));
        var runtime = OriginalRuntimeCatalog.FromJson(
            Extensions.PatchCatalog(catalogJson),
            correctOriginalEquipment: true,
            Extensions.LegacySkills,
            Extensions.ConfigureItem);
        Extensions.ConfigureRuntime(runtime);
        if (string.IsNullOrWhiteSpace(pack.Manifest.Parent))
        {
            runtime.VerifySources(name =>
            {
                byte[] bytes = _read("original/" + name);
                return string.Equals(name, "def_commander.xml", StringComparison.Ordinal)
                    ? LegacyTableXmlRepair.NormalizeCommanderTable(bytes)
                    : bytes;
            });
        }

        Countries.ValidateRuntime(runtime);
        var buildings = OriginalBuildingCatalog.Read(Text("def_construction.xml"), Text("def_capital.xml"));
        var combat = OriginalCombatCatalog.Read(
            System.Text.Encoding.UTF8.GetString(_read("data/runtime_rule_tables.json")),
            Text("def_terraintype.xml"),
            Text("def_installation.xml"),
            buildings,
            Extensions.LegacySkills);
        var accounts = OriginalCommanderAccounts.Read(
            runtime,
            combat,
            System.Text.Encoding.UTF8.GetString(_read("data/metagame_rule_tables.json")));
        Catalogs = new(
            runtime,
            OriginalTerrainRules.FromXml(Text("def_mapterrain.xml"), Text("def_terraintype.xml")),
            buildings,
            OriginalRecruitmentCatalog.Read(Text("def_card.xml")),
            combat,
            accounts,
            OriginalItemTradingCatalog.Read(Text("def_item.xml"), true, runtime.Items),
            OriginalWarzoneTechnology.Read(Text("def_warzonetech.xml")),
            OriginalDialogueCatalog.Read(Text("def_dialogues.xml"), Text("stringtable_cn.ini")));
        var definitions = XDocument.Parse(Text("def_map.xml")).Root!.Elements("map").ToArray();
        Maps = definitions.ToDictionary(
            e => (int)e.Attribute("id")!,
            e => OriginalTerrainMap.Read(_read("original/" + (string)e.Attribute("file")!)));
        Cities = XDocument.Parse(Text("def_areaname.xml")).Root!.Elements("areaname")
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

        Strings = strings;
        CityNamesByScenario = BuildScenarioCityNames(Text);
    }

    public CountryCatalog Countries { get; }
    public ExtensionLoader Extensions { get; }
    public OriginalScenarioCatalogs Catalogs { get; }
    public IReadOnlyDictionary<int, OriginalTerrainMap> Maps { get; }
    public IReadOnlyDictionary<int, string> Cities { get; }
    public IReadOnlyDictionary<string, string> Strings { get; }
    public IReadOnlyDictionary<string, IReadOnlyDictionary<int, string>> CityNamesByScenario { get; }

    public string Label(string key) => Strings.TryGetValue(key, out string? label) ? label : Strings.TryGetValue("name_" + key, out label) ? label : key;

    public string CountryName(string id) => Countries.Get(id)?.Name ?? Label(id);

    public OriginalBattle ReadBattle(byte[] btlBytes) => OriginalBattle.Read(btlBytes);

    private IReadOnlyDictionary<string, IReadOnlyDictionary<int, string>> BuildScenarioCityNames(Func<string, string> text)
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

            var names = ScenarioCityNames.Read(text(file), Cities);
            namesByScenario.Add(Path.GetFileNameWithoutExtension((string)entry.Attribute("file")!), names);
            if (entry.Attribute("file2") is XAttribute alternate)
            {
                namesByScenario.Add(Path.GetFileNameWithoutExtension(alternate.Value), names);
            }
        }

        return namesByScenario;
    }
}
