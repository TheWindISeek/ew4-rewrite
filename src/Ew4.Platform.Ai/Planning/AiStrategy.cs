using System;
using System.Collections.Generic;
using System.Linq;

namespace Ew4.Platform.Ai;

using Ew4.Platform.Engine;

public enum AiStrategyBehavior { VariedHalf, Full, VariedQuarter, Zero }
public sealed record AiStrategyUnit(int Health, int MaximumHealth, int Movement, bool Naval, bool Transport,
    bool Commander, bool AtSea, long Strength, long Threat);
public sealed record AiStrategyGoal(long LocationValue, long Strength, bool HasUnit, bool NavalUnit, bool Sea, bool NavalReachable);
public sealed record AiStrategyGoalOption(int Cell, AiStrategyGoal Facts);
public sealed record AiStrategyDestination(int Cell, long GoalDistance, int OriginDistance, bool Occupied = false,
    int Environment = 0, bool Allied = false, string? BuildingKind = null);
public sealed record AiStrategyLandingRules
{
    public bool AvoidHazards { get; init; } = true;
    public bool AllowDistantAlliedLanding { get; init; }
    public IReadOnlyCollection<string> ReservedAlliedBuildings { get; init; } = new[] { "0", "1", "2", "3" };
    public IReadOnlyDictionary<string, int> AlliedBuildingChance { get; init; } = new Dictionary<string, int> { ["4"] = 6 };
    public void Validate()
    {
        if (ReservedAlliedBuildings.Any(string.IsNullOrWhiteSpace)
            || AlliedBuildingChance.Any(p => string.IsNullOrWhiteSpace(p.Key) || p.Value < 0 || p.Value > 100))
            throw new RuleViolation("ai.strategy_landing", "Invalid allied landing policy");
    }
}
public sealed record AiStrategyRules
{
    public int HealthyHealthThreshold { get; init; } = 119;
    public long BaseScore { get; init; } = 10;
    public long HealthyBaseScore { get; init; } = 30;
    public long FullHealthBonus { get; init; } = 10;
    public long TransportOrNavalBonus { get; init; } = 20;
    public long CommanderBonus { get; init; } = 20;
    public long MinimumEmptyLandGoalValue { get; init; } = 44;
    public long NavalReachableUnitBonus { get; init; } = 30;
    public long NavalUnreachablePenalty { get; init; } = -120;
    public long EmbarkedVersusNavalPenalty { get; init; } = -30;
    public long EmbarkedSeaGoalBonus { get; init; } = 30;
    public long EmbarkedLandGoalBonus { get; init; } = 60;
    public long LandGoalBonus { get; init; } = 30;
    public long LandVersusEmbarkedBonus { get; init; } = 15;
    public long LandVersusNavalBonus { get; init; } = 7;
    public int MovementDistanceDivisor { get; init; } = 2;
    public void Validate()
    {
        if (HealthyHealthThreshold < 0 || MovementDistanceDivisor < 1 || MinimumEmptyLandGoalValue < 0)
            throw new RuleViolation("ai.strategy_configuration", "Invalid strategic goal or distance configuration");
    }
}

public static class AiStrategy
{
    private static void Validate(AiStrategyUnit unit, AiStrategyGoal goal, AiStrategyRules rules)
    {
        if (unit.Health < 1 || unit.Health > unit.MaximumHealth || unit.Movement < 0 || unit.Strength < 0 || unit.Threat < 0
            || goal.LocationValue < 0 || goal.Strength < 0)
            throw new RuleViolation("ai.strategy_facts", "Invalid strategic unit or goal facts");
    }

    public static bool Eligible(AiStrategyUnit unit, AiStrategyGoal goal, AiStrategyRules rules)
    {
        Validate(unit, goal, rules);
        return unit.Naval || goal.LocationValue > rules.MinimumEmptyLandGoalValue || goal.HasUnit;
    }

    public static long GoalScore(AiStrategyUnit unit, AiStrategyGoal goal, AiStrategyRules rules)
    {
        Validate(unit, goal, rules);
        long score = checked(goal.LocationValue / 2 + goal.Strength);
        long bonus = unit.Naval ? goal.HasUnit && goal.NavalReachable ? rules.NavalReachableUnitBonus : rules.NavalUnreachablePenalty
            : unit.AtSea ? goal.HasUnit && goal.NavalUnit ? rules.EmbarkedVersusNavalPenalty : goal.Sea ? rules.EmbarkedSeaGoalBonus : rules.EmbarkedLandGoalBonus
            : goal.HasUnit && goal.Sea ? goal.NavalUnit ? rules.LandVersusNavalBonus : rules.LandVersusEmbarkedBonus : rules.LandGoalBonus;
        return checked(score + bonus);
    }

    public static long Weight(AiStrategyUnit unit, AiStrategyGoal goal, int distance, AiStrategyRules rules)
    {
        Validate(unit, goal, rules);
        long basis = unit.Health > rules.HealthyHealthThreshold ? rules.HealthyBaseScore : rules.BaseScore;
        if (unit.Health == unit.MaximumHealth) basis = checked(basis + rules.FullHealthBonus);
        if (unit.Naval || unit.Transport) basis = checked(basis + rules.TransportOrNavalBonus);
        if (unit.Commander) basis = checked(basis + rules.CommanderBonus);
        return WeightScore(checked(basis + GoalScore(unit, goal, rules)), unit, distance, rules);
    }

    public static long WeightScore(long score, AiStrategyUnit unit, int distance, AiStrategyRules rules)
    {
        Validate(unit, new(0, 0, false, false, false, false), rules);
        if (distance < 0) throw new RuleViolation("ai.strategy_distance", "Negative strategic distance");
        if (unit.Threat != 0)
        {
            if (unit.Strength >= unit.Threat) score /= 2;
            else if (unit.Strength > unit.Threat / 2) score /= 3;
            else if (unit.Strength < unit.Threat / 2) score /= 4;
        }
        score = checked((long)((float)score * ((float)unit.Health / unit.MaximumHealth)));
        int step = unit.Movement / rules.MovementDistanceDivisor;
        long turns = step == 0 ? 0 : distance / step;
        return score / checked(turns + 1);
    }

    public static int SelectGoal(AiStrategyUnit unit, IReadOnlyList<AiStrategyGoalOption> goals, AiStrategyRules rules)
    {
        int selected = -1;
        long best = long.MinValue;
        for (int i = 0; i < goals.Count; i++)
        {
            if (goals[i].Cell < 0) throw new RuleViolation("ai.strategy_goal", "Invalid strategic goal cell");
            if (!Eligible(unit, goals[i].Facts, rules)) continue;
            long score = GoalScore(unit, goals[i].Facts, rules);
            if (score > best) { best = score; selected = i; }
        }
        return selected;
    }

    public static int SelectDestination(IReadOnlyList<AiStrategyDestination> destinations, bool adjacentOnly,
        AiStrategyLandingRules rules, Func<long, long> draw)
    {
        int selected = -1;
        long nearest = long.MaxValue;
        for (int i = 0; i < destinations.Count; i++)
        {
            var site = destinations[i];
            if (site.Cell < 0 || site.GoalDistance < 0 || site.OriginDistance < 0 || site.Environment < 0)
                throw new RuleViolation("ai.strategy_destination", "Invalid strategic destination facts");
            if (site.Occupied || rules.AvoidHazards && site.Environment > 0) continue;
            if (site.Allied)
            {
                if (!rules.AllowDistantAlliedLanding && site.OriginDistance != 1
                    || site.BuildingKind != null && rules.ReservedAlliedBuildings.Contains(site.BuildingKind)) continue;
                if (site.BuildingKind != null && rules.AlliedBuildingChance.TryGetValue(site.BuildingKind, out int chance))
                {
                    long value = draw(100);
                    if (value < 0 || value >= 100) throw new RuleViolation("ai.strategy_random", "Invalid allied landing draw");
                    if (value >= chance) continue;
                }
            }
            if (site.GoalDistance < nearest && (!adjacentOnly || site.OriginDistance == 1))
            { nearest = site.GoalDistance; selected = i; }
        }
        return selected;
    }

    public static long RandomBound(long score, AiStrategyBehavior behavior)
    {
        if (score < 0 || !Enum.IsDefined(typeof(AiStrategyBehavior), behavior))
            throw new RuleViolation("ai.strategy_behavior", "Invalid strategic random weighting");
        return behavior switch { AiStrategyBehavior.VariedHalf => score / 2, AiStrategyBehavior.VariedQuarter => score-score/4, _ => 0 };
    }

    public static long ApplyBehavior(long score, AiStrategyBehavior behavior, long randomDraw)
    {
        long bound = RandomBound(score, behavior);
        if (randomDraw < 0 || randomDraw >= Math.Max(1, bound)) throw new RuleViolation("ai.strategy_random", "Random weighting draw is outside its bound");
        return behavior switch
        {
            AiStrategyBehavior.VariedHalf => checked(score / 2 + randomDraw),
            AiStrategyBehavior.VariedQuarter => checked(score / 4 + randomDraw),
            AiStrategyBehavior.Zero => 0, _ => score
        };
    }
}

// SplitMix64 keeps full-width AI randomness separate from the recovered combat LCG.
public sealed class AiRandom
{
    public ulong State { get; private set; }
    public AiRandom(ulong seed) => State = seed;
    private ulong NextWord()
    {
        State = unchecked(State + 0x9e3779b97f4a7c15UL);
        ulong word = State;
        word = unchecked((word ^ (word >> 30)) * 0xbf58476d1ce4e5b9UL);
        word = unchecked((word ^ (word >> 27)) * 0x94d049bb133111ebUL);
        return word ^ (word >> 31);
    }
    public long Next(long bound)
    {
        if (bound < 0) throw new RuleViolation("ai.random_bound", "Negative AI random bound");
        if (bound == 0) return 0;
        ulong range = (ulong)bound, threshold = unchecked(0UL-range) % range, word;
        do { word = NextWord(); } while (word < threshold);
        return (long)(word % range);
    }
}
