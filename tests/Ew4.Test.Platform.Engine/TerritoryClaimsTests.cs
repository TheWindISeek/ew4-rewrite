using Ew4.Platform.Engine;
using Xunit;

namespace Ew4.Test.Platform.Engine;

public sealed class TerritoryClaimsTests
{
    [Fact]
    public void Move_does_not_paint_cells_occupied_by_other_units()
    {
        var map = new HexMap(3, 3);
        var cells = Enumerable.Range(0, map.Count).Select(_ => new CellState { Terrain = new MovementCell() }).ToArray();
        int start = 0, mid = 1, end = 2;
        var route = new[] { start, mid, end };
        int blocked = map.Neighbor(mid, 0);
        if (blocked < 0 || blocked == start || blocked == end) blocked = map.Neighbor(mid, 1);
        Assert.True(blocked >= 0);
        var result = TerritoryClaims.Move(map, cells, new[] { blocked }, route, "a");
        Assert.Equal("a", result[end].Owner);
        Assert.Null(result[blocked].Owner);
        int paintedNeighbor = Enumerable.Range(0, 6).Select(d => map.Neighbor(end, d)).First(n => n >= 0 && n != blocked && result[n].Owner == "a");
        Assert.True(paintedNeighbor >= 0);
    }

    [Fact]
    public void Move_still_paints_endpoint_when_adjacent_cell_has_unit()
    {
        var map = new HexMap(3, 3);
        var cells = Enumerable.Range(0, map.Count).Select(_ => new CellState { Terrain = new MovementCell() }).ToArray();
        int start = 0, end = 2;
        var route = new[] { start, end };
        int adjacent = map.Neighbor(end, 0);
        if (adjacent < 0 || adjacent == start) adjacent = map.Neighbor(end, 1);
        Assert.True(adjacent >= 0);
        var occupied = new[] { adjacent };
        var result = TerritoryClaims.Move(map, cells, occupied, route, "a");
        Assert.Equal("a", result[end].Owner);
        Assert.Null(result[adjacent].Owner);
    }

    [Fact]
    public void Move_paints_city_endpoint()
    {
        var map = new HexMap(3, 3);
        var cells = Enumerable.Range(0, map.Count).Select(i => new CellState
        {
            Terrain = new MovementCell(),
            BuildingKind = i == 2 ? "city" : null,
            Owner = i == 2 ? "b" : null,
        }).ToArray();
        var result = TerritoryClaims.Move(map, cells, Array.Empty<int>(), new[] { 0, 2 }, "a");
        Assert.Equal("a", result[2].Owner);
    }
}
