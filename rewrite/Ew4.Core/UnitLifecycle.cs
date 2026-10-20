using System;
using System.Collections.Generic;
using System.Linq;

namespace Ew4.Core;

public sealed record SurvivalAsset(bool HasUnit, string? BuildingKind);
public sealed record SurvivalRules(bool Units, bool Buildings, IReadOnlyCollection<string> ExcludedBuildings);

public static class FactionSurvival
{
    public static bool Defeated(IEnumerable<SurvivalAsset> assets, SurvivalRules rules) =>
        !assets.Any(a => (rules.Units && a.HasUnit) || (rules.Buildings && a.BuildingKind != null && !rules.ExcludedBuildings.Contains(a.BuildingKind)));
    public static SurvivalRules Legacy(int mode) => new(mode == 1 || mode == 2, mode == 0 || mode == 2, new[] { "farmland" });
}

public sealed record UnitRefreshInput
{
    public int Health { get; init; }
    public int MaximumHealth { get; init; }
    public int BaseHealth { get; init; }
    public int HealthBonus { get; init; }
    public int BaseMovement { get; init; }
    public int MovementBonus { get; init; }
    public MoraleState Morale { get; init; } = new();
    public int LocalPenalty { get; init; }
    public int Shortage { get; init; }
    public bool Logistics { get; init; }
    public bool Leadership { get; init; }
    public int ConstructionTurns { get; init; }
    public string Behavior { get; init; } = "default";
    public string RestoreBehavior { get; init; } = "default";
    public int? BehaviorExpiry { get; init; }
    public int Round { get; init; } = 1;
}
public sealed record UnitRefreshRules(int MinimumLivingHealth = 1, bool RestoreBehaviorAfterMissedRound = true);
public sealed record UnitRefreshResult(int Health, int MaximumHealth, int Movement, MoraleState Morale,
    bool Acted, int ConstructionTurns, string Behavior, int? BehaviorExpiry);

public static class UnitLifecycle
{
    public static UnitRefreshResult Refresh(UnitRefreshInput input, UnitRefreshRules? configuration = null)
    {
        var rules = configuration ?? new();
        input.Morale.Validate();
        if (input.Health < 1 || input.Health > input.MaximumHealth || input.BaseHealth < 1 || input.HealthBonus < 0
            || input.BaseMovement < 0 || input.MovementBonus < 0 || input.ConstructionTurns < 0 || input.Round < 1
            || input.BehaviorExpiry < 1 || string.IsNullOrWhiteSpace(input.Behavior) || string.IsNullOrWhiteSpace(input.RestoreBehavior)
            || rules.MinimumLivingHealth < 0 || rules.MinimumLivingHealth > 1)
            throw new RuleViolation("refresh.state", "Invalid unit refresh state");
        int maximum = checked(input.BaseHealth + input.HealthBonus);
        int health = Math.Min(maximum, Math.Max(rules.MinimumLivingHealth, (int)((long)input.Health * maximum / input.MaximumHealth)));
        int duration = Math.Max(0, input.Morale.Duration - 1);
        int baseline = input.Morale.Duration == 1 ? 0 : input.Morale.Baseline;
        int morale = TurnEconomy.Morale(baseline, duration, input.LocalPenalty, input.Shortage, input.Logistics, input.Leadership);
        bool restore = input.BehaviorExpiry.HasValue && (rules.RestoreBehaviorAfterMissedRound
            ? input.Round >= input.BehaviorExpiry.Value : input.Round == input.BehaviorExpiry.Value);
        return new(health, maximum, checked(input.BaseMovement + (input.BaseMovement > 0 ? input.MovementBonus : 0)),
            new(baseline, duration, morale), false, Math.Max(0, input.ConstructionTurns - 1),
            restore ? input.RestoreBehavior : input.Behavior, restore ? null : input.BehaviorExpiry);
    }
}

public sealed record HazardTransition(int NextIntensity, int Probability);
public sealed record HazardStage(int DamagePercent, IReadOnlyList<HazardTransition> Transitions);
public sealed record HazardRules
{
    public IReadOnlyDictionary<int, HazardStage> Stages { get; init; } = new Dictionary<int, HazardStage>();
    public bool Nonlethal { get; init; } = true;
    public void Validate()
    {
        foreach (var stage in Stages)
            if (stage.Key < 1 || stage.Value.DamagePercent < 0 || stage.Value.DamagePercent > 100
                || stage.Value.Transitions.Any(t => t.NextIntensity < 0 || (t.NextIntensity != 0 && !Stages.ContainsKey(t.NextIntensity)) || t.Probability < 0)
                || stage.Value.Transitions.Sum(t => (long)t.Probability) != 100)
                throw new RuleViolation("hazard.configuration", "Invalid hazard stage or decay distribution");
    }
}
public sealed record HazardResult(int Health, int Intensity, int Movement, bool Acted, ulong RandomState);

public static class Hazards
{
    public static int Damage(int health, int maximum, int percentage, bool nonlethal = true)
    {
        if (health < 0 || health > maximum || maximum < 1 || percentage < 0 || percentage > 100)
            throw new RuleViolation("hazard.health", "Invalid hazard health or damage percentage");
        return health == 0 ? 0 : Math.Max(nonlethal ? 1 : 0, health - (int)((long)maximum * percentage / 100));
    }
    public static HazardResult EndUnit(int health, int maximum, int intensity, Ew4Random random, HazardRules rules)
    {
        rules.Validate();
        if (intensity < 0 || (intensity != 0 && !rules.Stages.ContainsKey(intensity)))
            throw new RuleViolation("hazard.stage", "Unknown hazard intensity");
        int result = Damage(health, maximum, intensity == 0 ? 0 : rules.Stages[intensity].DamagePercent, rules.Nonlethal);
        int next = Decay(intensity, random, rules);
        return new(result, next, 0, true, random.State);
    }
    public static int Decay(int intensity, Ew4Random random, HazardRules rules)
    {
        rules.Validate();
        if (intensity < 0 || (intensity != 0 && !rules.Stages.ContainsKey(intensity)))
            throw new RuleViolation("hazard.stage", "Unknown hazard intensity");
        if (intensity > 0)
        {
            int roll = random.Next(100), threshold = 0;
            foreach (var transition in rules.Stages[intensity].Transitions)
            {
                threshold += transition.Probability;
                if (roll >= threshold) continue;
                return transition.NextIntensity;
            }
        }
        return 0;
    }
    public static HazardRules LegacyBurning() => new()
    {
        Stages = new Dictionary<int, HazardStage>
        {
            [1] = new(4, new[] { new HazardTransition(0, 100) }),
            [2] = new(6, new[] { new HazardTransition(1, 55), new HazardTransition(0, 45) }),
            [3] = new(8, new[] { new HazardTransition(2, 50), new HazardTransition(1, 30), new HazardTransition(0, 20) })
        }
    };
}
