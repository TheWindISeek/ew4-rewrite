using System;
using System.Collections.Generic;
using System.Linq;

namespace Ew4.Platform.Ai;

using Ew4.Platform.Engine;

public sealed record AiUnitPlan(long ExpectedRevision, string UnitId, ulong InitialRandomState, ulong FinalRandomState,
    IReadOnlyList<AiCandidate<WorldAiCommand>> Candidates);

public static partial class WorldAi
{
    public static AiUnitPlan PlanUnit(WorldState state, string unitId, ulong aiRandom, WorldAiRules rules)
    {
        var faction = WorldActions.Ready(state, state.Revision);
        var unit = state.Units.SingleOrDefault(u => u.Id == unitId);
        if (unit == null || unit.Owner != faction.Id) throw new RuleViolation("ai.unit", "Unknown active faction unit");
        if (WorldDiplomacy.Dormant(state, faction.Id, rules.NeutralCamps))
            return new(state.Revision, unitId, aiRandom, aiRandom, Array.Empty<AiCandidate<WorldAiCommand>>());
        if (!rules.Behaviors.TryGetValue(unit.Behavior, out var behavior)) throw new RuleViolation("ai.behavior", "Unmapped unit AI behavior");
        var requests = AiPlanning.Requests(behavior, new(unit.RemainingMovement, unit.Morale.Effective, unit.Combatant.ConstructionTurns, unit.Acted));
        var result = new List<AiCandidate<WorldAiCommand>>();
        ulong finalRandom = aiRandom;
        foreach (var kind in requests.Kinds)
        {
            switch (kind)
            {
                case AiCandidateKind.Hold:
                    var facts = Facts(state, unit, rules);
                    var cell = state.Cells[unit.Cell];
                    long hold = AiPlanning.HoldScore(new(AiTactics.Location(Site(state, unit.Cell, rules), facts, rules.Tactics),
                        AiTactics.Strength(facts, rules.Tactics), ThreatStrength(state, unit, rules), cell.Environment,
                        facts.Category == rules.HoldInfantryCategory, cell.InstallationKind != null, facts.Category == rules.Tactics.NavalCategory,
                        facts.Health, facts.MaximumHealth), rules.Hold);
                    result.Add(new(new AiHoldCommand(state.Revision, unitId), hold));
                    break;
                case AiCandidateKind.TacticalMove:
                    foreach (var candidate in TacticalMoveCandidates(state, unitId, rules)) result.Add(new(candidate.Action, candidate.Score));
                    break;
                case AiCandidateKind.StrategicAdvance:
                    var strategic = StrategicCandidates(state, unitId, aiRandom, rules);
                    finalRandom = strategic.FinalRandomState;
                    foreach (var candidate in strategic.Candidates) result.Add(new(candidate.Action, candidate.Score));
                    break;
                case AiCandidateKind.Strike:
                    foreach (var candidate in StrikeCandidates(state, unitId, rules)) result.Add(new(candidate.Action, candidate.Score));
                    break;
            }
        }
        if (requests.Eligible && result.Count == 0) result.Add(new(new AiHoldCommand(state.Revision, unitId), 0));
        return new(state.Revision, unitId, aiRandom, finalRandom, result.ToArray());
    }

    public static AiUnitPlan? PlanNextUnit(WorldState state, ulong aiRandom, WorldAiRules rules)
    {
        var faction = WorldActions.Ready(state, state.Revision);
        foreach (var unit in state.Units.Where(u => u.Owner == faction.Id).OrderBy(u => u.Cell).ThenBy(u => u.Id, StringComparer.Ordinal))
        {
            var plan = PlanUnit(state, unit.Id, aiRandom, rules);
            if (plan.Candidates.Count > 0) return plan;
        }
        return null;
    }
}
