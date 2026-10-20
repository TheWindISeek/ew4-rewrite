using Ew4.Core;

namespace Ew4.Legacy;

public static class LegacyMapDefinitions
{
    public static WorldMapDefinition Import(LegacyTerrainMap source, LegacyTerrainRules terrain, string id, string name,
        IReadOnlyList<MapImageLayer>? images = null)
    {
        var cells = source.Cells.Select(c =>
        {
            var resolved = terrain.Resolve(c);
            return new CellState { Terrain = resolved.Movement, DefenseTerrain = resolved.DefenseTerrain, FireTerrain = resolved.FireTerrain };
        }).ToArray();
        return new(id, name, source.Width, source.Height, cells, images);
    }
}
