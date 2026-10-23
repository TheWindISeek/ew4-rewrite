using System;
using System.Collections.Generic;
using Ew4.Platform.Engine;

namespace Ew4.Platform.Content.Build;

/// <summary>
/// 从已激活 Pack 加载原版运行时目录（catalog、地图、规则表），供剧本开局使用。
/// </summary>
public sealed class PackRuntimeBootstrap
{
    public PackRuntimeBootstrap(IPackSource pack)
    {
        LegacyPackRuntimeLoadResult loaded = LegacyPackRuntimeLoader.Load(
            PackAssetRead.FromPack(pack),
            new LegacyPackRuntimeLoadOptions(
                VerifySourceHashes: string.IsNullOrWhiteSpace(pack.Manifest.Parent)));
        Countries = loaded.Countries;
        Extensions = loaded.Extensions;
        Catalogs = loaded.Catalogs;
        Maps = loaded.Maps;
        Cities = loaded.Cities;
        Strings = loaded.Strings;
        CityNamesByScenario = loaded.CityNamesByScenario;
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
}
