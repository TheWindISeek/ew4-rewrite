using System.Security.Cryptography;
using Ew4.Platform.Content.CatalogLoad;
using Ew4.Platform.Content.StaticData;

namespace Ew4.Platform.Content.Maps;

public sealed record MapCrop(int X, int Y, int W, int H);

public sealed record MapSummary(int MapId, string Name, int Width, int Height, string Hash);

public sealed record MapPreviewCell(int BackgroundKind, int BackgroundIndex, int ForegroundKind, int ForegroundIndex, int RiverEdges);

public sealed record MapPreview(int MapId, MapCrop Crop, IReadOnlyList<MapPreviewCell> Cells);

/// <summary>地图选择模块：列出 base 里可用的底图，描述尺寸与内容 hash，预览裁剪区域。</summary>
public interface IMapCatalog
{
    IReadOnlyList<MapSummary> List();

    MapSummary? Describe(int mapId);

    /// <summary>crop 省略表示整图；crop 越界时返回 null。</summary>
    MapPreview? Preview(int mapId, MapCrop? crop = null);
}

public sealed class MapCatalog : IMapCatalog
{
    private readonly IReadOnlyDictionary<int, StaticDataTerrainMap> _maps;
    private readonly IReadOnlyDictionary<int, MapSummary> _summaries;

    public MapCatalog(IReadOnlyDictionary<int, StaticDataTerrainMap> maps, IReadOnlyDictionary<int, string> names)
    {
        _maps = maps;
        _summaries = maps.ToDictionary(
            p => p.Key,
            p => new MapSummary(
                p.Key,
                names.TryGetValue(p.Key, out string? name) ? name : "map" + p.Key,
                p.Value.Width,
                p.Value.Height,
                Convert.ToHexStringLower(Ew4.Interface.Common.Sha256Hash.HashData(p.Value.ToBytes()))));
    }

    public static MapCatalog FromMod(IModSource pack)
    {
        var bootstrap = new ModRuntimeBootstrap(pack);
        return new MapCatalog(bootstrap.Maps, bootstrap.MapNames);
    }

    public IReadOnlyList<MapSummary> List() => _summaries.Values.OrderBy(s => s.MapId).ToArray();

    public MapSummary? Describe(int mapId) => _summaries.TryGetValue(mapId, out MapSummary? summary) ? summary : null;

    public MapPreview? Preview(int mapId, MapCrop? crop = null)
    {
        if (!_maps.TryGetValue(mapId, out StaticDataTerrainMap? map)) return null;
        MapCrop area = crop ?? new MapCrop(0, 0, map.Width, map.Height);
        if (area.W < 1 || area.H < 1 || area.X < 0 || area.Y < 0 || area.X + area.W > map.Width || area.Y + area.H > map.Height) return null;
        var cells = new List<MapPreviewCell>(area.W * area.H);
        for (int y = 0; y < area.H; y++)
            for (int x = 0; x < area.W; x++)
            {
                StaticDataTerrainCell c = map.Cells[(area.Y + y) * map.Width + area.X + x];
                cells.Add(new(c.BackgroundKind, c.BackgroundIndex, c.ForegroundKind, c.ForegroundIndex, c.RiverEdges));
            }

        return new(mapId, area, cells);
    }
}
