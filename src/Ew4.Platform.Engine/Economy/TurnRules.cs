using System;
using System.Collections.Generic;
using System.Linq;

namespace Ew4.Platform.Engine;

public sealed record Resources(long Money, long Industry, long Food)
{
    public void Validate()
    {
        if (Money < 0 || Industry < 0 || Food < 0) throw new RuleViolation("resources.negative", "Resources must be nonnegative");
    }
}

public sealed record EconomyResult(Resources Resources, int Shortage);

public static class TurnEconomy
{
    public static EconomyResult Start(int round, Resources current, Resources income, long foodConsumption, long? resourceCap = null,
        int previousShortage = 0, bool allowResourceDebt = false)
    {
        if (!allowResourceDebt) current.Validate();
        income.Validate();
        if (round < 1 || foodConsumption < 0 || resourceCap < 0 || previousShortage < 0)
            throw new RuleViolation("economy.configuration", "Invalid economy inputs");
        if (round == 1) return new EconomyResult(current, previousShortage);
        long Add(long a, long b)
        {
            long sum = checked(a + b);
            if (allowResourceDebt) sum = Math.Max(0, sum);
            return resourceCap.HasValue ? Math.Min(resourceCap.Value, sum) : sum;
        }
        long money = Add(current.Money, income.Money), industry = Add(current.Industry, income.Industry), food = Add(current.Food, income.Food);
        int shortage = 0;
        if (food < foodConsumption)
        {
            float ratio = (float)food / foodConsumption;
            shortage = ratio >= .75f ? 1 : ratio >= .5f ? 2 : ratio >= .25f ? 3 : 4;
            food = 0;
        }
        else food -= foodConsumption;
        return new EconomyResult(new Resources(money, industry, food), shortage);
    }

    public static int Morale(int baseline, int duration, int localPenaltyLevel, int shortage, bool logistics, bool leadership)
    {
        if (duration < 0 || localPenaltyLevel < 0 || localPenaltyLevel > 2 || shortage < 0)
            throw new RuleViolation("morale.configuration", "Invalid morale inputs");
        int result = baseline;
        if (duration < 3)
        {
            int penalty = -localPenaltyLevel;
            if (shortage >= 2 && !logistics) penalty = (int)Math.Max(-3L, (long)penalty - shortage + 1);
            result = checked((int)Math.Max(-3L, (long)baseline + penalty));
        }
        return leadership && result < 0 ? 0 : result;
    }
}

public sealed record FactionFacts(string Id, string Camp, bool Human, bool Defeated, int Objectives, bool Combatant = true);
public enum BattleOutcome { Continue, Victory, Defeat }
public enum ObjectiveUnitOwnership { UnitOwner, TerritoryOwner }
public sealed record VictoryRules(int? RoundLimit = null, bool ObjectivesEnabled = false,
    ObjectiveUnitOwnership UnitObjectiveOwnership = ObjectiveUnitOwnership.UnitOwner)
{
    public ExtensionVictoryRules? Custom { get; init; }
}

public static class Victory
{
    // Explicit viewpoint replaces the native dependency on the last visited human camp.
    public static BattleOutcome Evaluate(string playerCamp, int round, IReadOnlyCollection<FactionFacts> factions, VictoryRules rules)
    {
        if (round < 1 || rules.RoundLimit < 1 || !Enum.IsDefined(typeof(ObjectiveUnitOwnership), rules.UnitObjectiveOwnership) || factions.Any(f => f.Objectives < 0)
            || factions.Select(f => f.Id).Distinct(StringComparer.Ordinal).Count() != factions.Count)
            throw new RuleViolation("victory.state", "Invalid victory state");
        if (rules.RoundLimit.HasValue && round > rules.RoundLimit.Value) return BattleOutcome.Defeat;
        var alive = factions.Where(f => !f.Defeated && f.Combatant).ToArray();
        if (!alive.Any(f => f.Human && f.Camp == playerCamp)) return BattleOutcome.Defeat;
        var enemies = alive.Where(f => f.Camp != playerCamp).ToArray();
        if (enemies.Length == 0) return BattleOutcome.Victory;
        if (!rules.ObjectivesEnabled) return BattleOutcome.Continue;
        if (enemies.Sum(f => (long)f.Objectives) == 0) return BattleOutcome.Victory;
        return alive.Where(f => f.Camp == playerCamp).Sum(f => (long)f.Objectives) > 0 ? BattleOutcome.Continue : BattleOutcome.Defeat;
    }
}
