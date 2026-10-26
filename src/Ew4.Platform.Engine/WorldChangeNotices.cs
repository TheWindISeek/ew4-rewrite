using System.Collections.Generic;
using System.Linq;

namespace Ew4.Platform.Engine;

public static class WorldChangeNotices
{
    public static IReadOnlyList<WorldNotice> FromStates(WorldState before, WorldState after)
    {
        var notices = new List<WorldNotice>();
        foreach (var unit in before.Units)
        {
            var next = after.Units.SingleOrDefault(u => u.Id == unit.Id);
            int damage = unit.Combatant.Health - (next?.Combatant.Health ?? 0);
            if (damage > 0)
            {
                notices.Add(new("battle.damage", unit.Id, damage));
            }

            if (next == null)
            {
                notices.Add(new("unit.destroyed", unit.Id));
            }
            else if (next.Cell != unit.Cell)
            {
                notices.Add(new("unit.moved", unit.Id, next.Cell));
            }
        }

        foreach (var unit in after.Units.Where(u => !before.Units.Any(old => old.Id == u.Id)))
        {
            notices.Add(new("unit.recruited", unit.Id));
        }

        foreach (var commander in after.Commanders.Where(c => c.Withdrawn && before.Commanders.Any(old => old.Id == c.Id && !old.Withdrawn)))
        {
            notices.Add(new("commander.withdrawn", commander.Id));
        }

        foreach (var faction in after.Factions.Where(f => f.Defeated && before.Factions.Any(old => old.Id == f.Id && !old.Defeated)))
        {
            notices.Add(new("faction.defeated", faction.Id));
        }

        for (int i = 0; i < before.Cells.Count; i++)
        {
            if (before.Cells[i].Owner != after.Cells[i].Owner)
            {
                notices.Add(new("cell.owner_changed", i.ToString()));
            }
        }

        return notices;
    }
}
