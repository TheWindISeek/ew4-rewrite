using Ew4.Platform.Engine;
using Xunit;

namespace Ew4.Test.Platform.Engine;

public sealed class MovementSeaTests
{
    [Fact]
    public void Naval_unit_spends_sea_cost_per_ocean_cell()
    {
        var map = new HexMap(3, 1);
        var cells = new MovementCell[]
        {
            new() { Sea = false, TerrainCost = 3 },
            new() { Sea = true, TerrainCost = 3 },
            new() { Sea = true, TerrainCost = 3 },
        };
        var actor = new MovementActor { Category = "naval", Camp = "a", FullBudget = 2, Remaining = 2 };
        var rules = new MovementRules { BaseLandCost = 3, SeaCost = 1 };
        var reachable = Movement.Reachable(map, cells, 0, actor, rules);
        Assert.True(reachable.ContainsKey(1));
        Assert.True(reachable.ContainsKey(2));
        Assert.Equal(1, reachable[1].Remaining);
        Assert.Equal(0, reachable[2].Remaining);
    }

    [Fact]
    public void Infantry_without_transport_cannot_enter_sea()
    {
        var map = new HexMap(2, 1);
        var cells = new MovementCell[] { new() { Sea = false }, new() { Sea = true } };
        var actor = new MovementActor { Category = "infantry", Camp = "a", FullBudget = 3, Remaining = 3 };
        var reachable = Movement.Reachable(map, cells, 0, actor, new MovementRules());
        Assert.False(reachable.ContainsKey(1));
    }

    [Fact]
    public void Transported_land_unit_on_sea_spends_full_budget_per_ocean_cell()
    {
        var map = new HexMap(3, 1);
        var cells = new MovementCell[]
        {
            new() { Sea = true, TerrainCost = 3 },
            new() { Sea = true, TerrainCost = 3 },
            new() { Sea = true, TerrainCost = 3 },
        };
        var actor = new MovementActor { Category = "infantry", Camp = "a", Transport = true, FullBudget = 2, Remaining = 2 };
        var rules = new MovementRules { BaseLandCost = 3, SeaCost = 1 };
        var reachable = Movement.Reachable(map, cells, 0, actor, rules);
        Assert.True(reachable.ContainsKey(1));
        Assert.False(reachable.ContainsKey(2));
        Assert.Equal(0, reachable[1].Remaining);
    }

    [Fact]
    public void Transported_land_unit_embark_from_shore_costs_full_budget()
    {
        var map = new HexMap(2, 1);
        var cells = new MovementCell[] { new() { Sea = false, TerrainCost = 3 }, new() { Sea = true, TerrainCost = 3 } };
        var actor = new MovementActor { Category = "infantry", Camp = "a", Transport = true, FullBudget = 2, Remaining = 2 };
        var reachable = Movement.Reachable(map, cells, 0, actor, new MovementRules { SeaCost = 1 });
        Assert.True(reachable.ContainsKey(1));
        Assert.Equal(0, reachable[1].Remaining);
    }
}
