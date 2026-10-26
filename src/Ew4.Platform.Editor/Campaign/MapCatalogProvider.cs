using System.Collections.Concurrent;
using Ew4.Platform.Content;
using Ew4.Platform.Content.Maps;

namespace Ew4.Platform.Editor.Campaign;

/// <summary>按 mod 的 parent（无 parent 则按自身）缓存地图目录；加载地图表较重，不要每次编辑都重建。</summary>
public sealed class MapCatalogProvider
{
    private readonly ConcurrentDictionary<string, IMapCatalog> _cache = new(StringComparer.Ordinal);

    public IMapCatalog Get(IModSource merged) =>
        _cache.GetOrAdd(merged.Manifest.Parent ?? merged.Manifest.Id, _ => MapCatalog.FromMod(merged));
}
