using System;

namespace Ew4.Platform.Ai;

using Ew4.Platform.Engine;

public enum AiTerritoryRelation { Hostile, NonAllied, Owned, Allied, Unclassified }
public sealed record AiMoveFacts(long SourceValue, long DestinationValue, AiTerritoryRelation Relation,
    int Environment = 0, int BuildingLevel = 0, bool Wounded = false, bool HumanOwned = false);
public sealed record AiMoveScoringRules
{
    public long CaptureBonus { get; init; } = 25;
    public long AlliedPenalty { get; init; } = -10000;
    public long EnvironmentPenalty { get; init; } = 25;
    public long WoundedBuildingBonusPerLevel { get; init; } = 10;
    public long HumanTerritoryBonus { get; init; } = 40;
    public float FullAttackWeightAtHealthFraction { get; init; } = .35f;
    public bool PenalizeHazardsWhenAttacking { get; init; }
    public void Validate()
    {
        if (CaptureBonus < 0 || EnvironmentPenalty < 0 || WoundedBuildingBonusPerLevel < 0 || HumanTerritoryBonus < 0
            || float.IsNaN(FullAttackWeightAtHealthFraction) || FullAttackWeightAtHealthFraction <= 0 || FullAttackWeightAtHealthFraction > 1)
            throw new RuleViolation("ai.move_configuration", "Invalid tactical movement scoring");
    }
}

public static class AiMovement
{
    private static void Validate(AiMoveFacts facts, AiMoveScoringRules rules)
    {
        if (facts.SourceValue < 0 || facts.DestinationValue < 0 || facts.Environment < 0 || facts.BuildingLevel < 0
            || !Enum.IsDefined(typeof(AiTerritoryRelation), facts.Relation))
            throw new RuleViolation("ai.move_facts", "Invalid move scoring facts");
    }
    private static long Territory(long delta, AiTerritoryRelation relation, bool attack, AiMoveScoringRules rules) => relation switch
    {
        AiTerritoryRelation.Hostile => checked((delta + rules.CaptureBonus) * 2),
        AiTerritoryRelation.NonAllied => checked((attack ? delta : delta / 2) + rules.CaptureBonus),
        AiTerritoryRelation.Owned => delta / (attack ? 2 : 4),
        AiTerritoryRelation.Allied => rules.AlliedPenalty,
        _ => 0
    };

    public static long MoveScore(AiMoveFacts facts, AiMoveScoringRules rules)
    {
        Validate(facts, rules);
        long score = Territory(Math.Max(0, facts.DestinationValue-facts.SourceValue), facts.Relation, false, rules);
        return checked(score - facts.Environment * rules.EnvironmentPenalty
            + (facts.Wounded ? facts.BuildingLevel * rules.WoundedBuildingBonusPerLevel : 0)
            + (facts.HumanOwned ? rules.HumanTerritoryBonus : 0));
    }

    public static long MoveAttackScore(AiMoveFacts facts, long attackScore, int health, int maximumHealth, AiMoveScoringRules rules)
    {
        Validate(facts, rules);
        if (health < 1 || health > maximumHealth) throw new RuleViolation("ai.unit", "Invalid move-attack health");
        long delta = facts.DestinationValue-facts.SourceValue;
        long score = checked(Territory(Math.Max(0, delta), facts.Relation, true, rules) + (delta < 0 ? attackScore / 2 : attackScore));
        if (rules.PenalizeHazardsWhenAttacking) score = checked(score - facts.Environment * rules.EnvironmentPenalty);
        float fraction = (float)health / maximumHealth;
        if (fraction >= rules.FullAttackWeightAtHealthFraction) fraction = 1;
        return checked((long)((float)score * fraction));
    }
}
