using System;
using System.Collections.Generic;

namespace Ew4.Platform.Ai;

using Ew4.Platform.Engine;

public enum AiCandidateKind { Hold, TacticalMove, Strike, StrategicAdvance }
public sealed record AiBehavior(bool Hold, bool TacticalMove, bool StrategicAdvance);
public sealed record AiUnitStatus(int RemainingMovement, int Morale, int ConstructionTurns, bool Acted);
public sealed record AiRequests(bool Eligible, IReadOnlyList<AiCandidateKind> Kinds);
public sealed record AiCandidate<T>(T Action, long Score);
public sealed record AiHoldFacts(long LocationValue, long OwnStrength, long ThreatStrength, int Environment,
    bool Infantry, bool InstallationPresent, bool Naval, int Health, int MaximumHealth);
public sealed record AiHoldRules(int EnvironmentPenalty = 25, int NavalPenalty = 15, int InstallationMultiplier = 2);

public static class AiPlanning
{
    public static AiRequests Requests(AiBehavior behavior, AiUnitStatus unit)
    {
        if (unit.RemainingMovement < 0 || unit.ConstructionTurns < 0 || unit.Morale < -3 || unit.Morale > 1)
            throw new RuleViolation("ai.unit", "Invalid planning state");
        var kinds = new List<AiCandidateKind>();
        bool mobile = unit.RemainingMovement > 0 && unit.Morale != -3;
        if (!mobile && (unit.ConstructionTurns > 0 || unit.Acted)) return new(false, kinds);
        if (behavior.Hold) kinds.Add(AiCandidateKind.Hold);
        if (mobile)
        {
            if (behavior.TacticalMove) kinds.Add(AiCandidateKind.TacticalMove);
            if (behavior.StrategicAdvance) kinds.Add(AiCandidateKind.StrategicAdvance);
        }
        if (unit.ConstructionTurns == 0 && !unit.Acted) kinds.Add(AiCandidateKind.Strike);
        return new(true, kinds);
    }

    // Strict comparison preserves generation order for equal scores.
    public static int Best<T>(IReadOnlyList<AiCandidate<T>> candidates, long? compatibilityFloor = null)
    {
        if (candidates.Count == 0) return -1;
        int selected = 0;
        long best = compatibilityFloor ?? candidates[0].Score;
        for (int i = 0; i < candidates.Count; i++)
            if (candidates[i].Score > best) { best = candidates[i].Score; selected = i; }
        return selected;
    }

    public static long HoldScore(AiHoldFacts facts, AiHoldRules? configuration = null)
    {
        var rules = configuration ?? new();
        if (facts.LocationValue < 0 || facts.OwnStrength < 0 || facts.ThreatStrength < 0 || facts.Environment < 0
            || facts.Health <= 0 || facts.Health > facts.MaximumHealth || rules.EnvironmentPenalty < 0
            || rules.NavalPenalty < 0 || rules.InstallationMultiplier < 0)
            throw new RuleViolation("ai.score", "Invalid scoring inputs");
        long score = facts.ThreatStrength == 0 ? facts.LocationValue / 2
            : facts.OwnStrength < facts.ThreatStrength ? (long)facts.LocationValue * 2 : facts.LocationValue;
        if (facts.Infantry && facts.InstallationPresent) score = checked(score * rules.InstallationMultiplier);
        score = Math.Max(0, checked(score - (long)facts.Environment * rules.EnvironmentPenalty - (facts.Naval ? rules.NavalPenalty : 0)));
        float vulnerability = 2f - (float)facts.Health / facts.MaximumHealth;
        return checked((long)((float)score * vulnerability));
    }
}
