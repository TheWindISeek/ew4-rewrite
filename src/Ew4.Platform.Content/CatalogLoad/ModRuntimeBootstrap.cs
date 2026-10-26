using System;
using System.Collections.Generic;
using Ew4.Platform.Engine;

namespace Ew4.Platform.Content.CatalogLoad;

/// <summary>
/// 从已激活 Pack 加载原版运行时目录（catalog、地图、规则表），供剧本开局使用。
/// </summary>
public sealed class ModRuntimeBootstrap
{
    public ModRuntimeBootstrap(IModSource pack)
    {
        LegacyModRuntimeLoadResult loaded = LegacyModRuntimeLoader.Load(
            ModAssetRead.FromMod(pack),
            new LegacyModRuntimeLoadOptions(
                VerifySourceHashes: string.IsNullOrWhiteSpace(pack.Manifest.Parent)));
        Countries = loaded.Countries;
        Catalogs = loaded.Catalogs;
        Maps = loaded.Maps;
        MapNames = loaded.MapNames;
        Cities = loaded.Cities;
        Strings = loaded.Strings;
        CityNamesByScenario = loaded.CityNamesByScenario;
    }

    public CountryCatalog Countries { get; }
    public StaticDataScenarioCatalogs Catalogs { get; }
    public IReadOnlyDictionary<int, StaticDataTerrainMap> Maps { get; }
    public IReadOnlyDictionary<int, string> MapNames { get; }
    public IReadOnlyDictionary<int, string> Cities { get; }
    public IReadOnlyDictionary<string, string> Strings { get; }
    public IReadOnlyDictionary<string, IReadOnlyDictionary<int, string>> CityNamesByScenario { get; }

    public string Label(string key) => Strings.TryGetValue(key, out string? label) ? label : Strings.TryGetValue("name_" + key, out label) ? label : key;

    public string CountryName(string id) => Countries.Get(id)?.Name ?? Label(id);
}
