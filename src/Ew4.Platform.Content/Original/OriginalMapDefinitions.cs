using Ew4.Platform.Engine;

namespace Ew4.Platform.Content.Original;

public static class OriginalMapDefinitions
{
    public static WorldMapDefinition Import(OriginalTerrainMap source, OriginalTerrainRules terrain, string id, string name,
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
