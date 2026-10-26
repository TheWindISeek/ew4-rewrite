using System;
using System.Collections.Generic;
using System.Linq;

using Ew4.Platform.Engine;

namespace Ew4.Platform.Content;

public static class ConsumableItems
{
    public static string? UnavailableReason(GameSession state, string unitId, Equipment item, TrainingRules training)
    {
        var world = state.World;
        var unit = world.Units.SingleOrDefault(u => u.Id == unitId);
        if (world.Phase != TurnPhase.Active || world.PendingDialogue != null || world.Outcome != BattleOutcome.Continue
            || world.ActiveFaction != state.PlayerFaction || !world.Factions.Single(f => f.Id == world.ActiveFaction).Human)
            return "当前无法使用物品";
        if (unit == null || unit.Owner != state.PlayerFaction) return "请选择己方部队";
        if (unit.Combatant.ConstructionTurns > 0) return "部队尚未完成建造";
        if (!item.Consumable || item.TargetCategory != null && item.TargetCategory != unit.Combatant.Profile.Category)
            return "此物品不能用于该部队";
        if (state.Account.Inventory.GetValueOrDefault(item.Id) < 1) return "物品数量不足";
        if (item.UseSkill != null) return "此物品效果不可用";
        return item.Effect switch
        {
            "ew4.item_effect.6" => unit.Morale.Effective < 1 ? null : "部队士气已经高昂",
            "ew4.item_effect.7" => unit.Morale.Effective < 0 ? null : "部队士气无需恢复",
            "ew4.item_effect.8" => item.Value > 0 && unit.Combatant.Health < unit.Combatant.Profile.MaxHealth ? null : "部队无需治疗",
            "ew4.item_effect.17" => training.Stages.Take(training.Stages.Count - 1).Any(s => s.Id == unit.Veterancy)
                ? null : "部队等级已满",
            _ => "此物品没有可用效果"
        };
    }

    public static SessionChange Use(GameSession state, long expectedRevision, string unitId, string itemId,
        ItemTradingRules items, TrainingRules training)
    {
        state.Validate(); items.Validate(); training.Validate();
        if (state.Revision != expectedRevision) throw new RuleViolation("session.revision", "物品状态已变化，请重新选择");
        WorldActions.Ready(state.World, state.World.Revision);
        if (!items.Definitions.TryGetValue(itemId, out var definition))
            throw new RuleViolation("item.unknown", "未知物品");
        var item = definition.Equipment;
        string? reason = UnavailableReason(state, unitId, item, training);
        if (reason != null) throw new RuleViolation("item.unavailable", reason);
        var unit = state.World.Units.Single(u => u.Id == unitId);
        var changed = unit;
        if (item.Effect is "ew4.item_effect.6" or "ew4.item_effect.7")
        {
            var morale = MoraleState.Set(item.Effect == "ew4.item_effect.6" ? 1 : 0, unit.Combatant.Has(Ew4Combat.Skill(3)));
            changed = unit with { Morale = morale, Combatant = unit.Combatant with { Morale = morale.Effective } };
        }
        else if (item.Effect == "ew4.item_effect.8")
            changed = unit with { Combatant = unit.Combatant with { Health = (int)Math.Min(unit.Combatant.Profile.MaxHealth,
                (long)unit.Combatant.Health + item.Value) } };
        else if (item.Effect == "ew4.item_effect.17")
        {
            int level = training.Stages.ToList().FindIndex(s => s.Id == unit.Veterancy) + 1;
            changed = unit with { Veterancy = training.Stages[level].Id, Experience = 0,
                Combatant = unit.Combatant with { Health = (int)Math.Min(unit.Combatant.Profile.MaxHealth,
                    (long)unit.Combatant.Health + training.Stages[level].HealingOnEntry) } };
        }
        var world = state.World with { Units = state.World.Units.Select(u => u.Id == unitId ? changed : u).ToArray() };
        var inventory = state.Account.Inventory.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        if (inventory[itemId] == 1) inventory.Remove(itemId); else inventory[itemId]--;
        var next = (state with
        {
            Revision = checked(state.Revision + 1),
            Account = state.Account.Copy() with { Revision = checked(state.Account.Revision + 1), Inventory = inventory },
            World = world with { Revision = checked(state.World.Revision + 1) }
        }).Copy();
        next.Validate();
        return new(next, new[] { new WorldNotice("unit.item_used", unitId) }.Concat(WorldChangeNotices.FromStates(state.World, next.World)).ToArray());
    }
}
