using System.Linq;

namespace Ew4.Platform.Engine;

/// <summary>单位原地待命：耗尽移动与行动，本回合不再行动。</summary>
public static class WorldHold
{
    public static WorldChange Hold(WorldState state, long expectedRevision, string unitId)
    {
        var faction = WorldActions.Ready(state, expectedRevision);
        var unit = state.Units.SingleOrDefault(u => u.Id == unitId);
        if (unit == null || unit.Owner != faction.Id) throw new RuleViolation("hold.unit", "Unknown active faction unit");
        var next = (state with
        {
            Revision = checked(state.Revision + 1),
            Units = state.Units.Select(u => u.Id == unit.Id ? u with { RemainingMovement = 0, Acted = true } : u).ToArray()
        }).Copy();
        next.Validate();
        return new(next, new[] { new WorldNotice("unit.held", unit.Id, unit.Cell) });
    }
}
