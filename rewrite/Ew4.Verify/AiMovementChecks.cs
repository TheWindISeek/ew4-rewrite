using System.Text.Json;
using Ew4.Core;
using Ew4.Persistence;

internal static class AiMovementChecks
{
    public static int Run(string root)
    {
        int checks = 0;
        void Check(bool value, string label)
        {
            checks++;
            if (!value) throw new InvalidOperationException(label);
        }
        void Reject(Action action, string code)
        {
            try { action(); }
            catch (RuleViolation e) { Check(e.Code == code, code + ": " + e.Code); return; }
            throw new InvalidOperationException("Expected " + code);
        }
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "reverse/reports/ai_movement_vectors.json")));
        var scoring = new AiMoveScoringRules { FullAttackWeightAtHealthFraction = data.RootElement.GetProperty("health_threshold").GetSingle() };
        foreach (var v in data.RootElement.GetProperty("fixtures").EnumerateArray())
        {
            int Int(string name) => v.GetProperty(name).GetInt32();
            bool Flag(string name) => v.GetProperty(name).GetBoolean();
            var relation = v.GetProperty("relation").GetString() switch
            {
                "hostile" => AiTerritoryRelation.Hostile, "nonallied" => AiTerritoryRelation.NonAllied,
                "owned" => AiTerritoryRelation.Owned, "allied" => AiTerritoryRelation.Allied,
                _ => throw new InvalidOperationException("Unknown relation")
            };
            var facts = new AiMoveFacts(Int("source_value"), Int("destination_value"), relation, Int("environment"), Flag("built") ? Int("level") : 0,
                Int("health") < Int("maximum"), Flag("has_owner") && Flag("human"));
            long moveScore = AiMovement.MoveScore(facts, scoring);
            Check(moveScore == Int("move_score"), "native tactical movement score");
            var actual = new List<int[]>();
            if (moveScore > 0) actual.Add([1, -1, checked((int)moveScore)]);
            if (v.GetProperty("combined_score").ValueKind != JsonValueKind.Null)
            {
                long combinedScore = AiMovement.MoveAttackScore(facts, Int("attack_score"), Int("health"), Int("maximum"), scoring);
                Check(combinedScore == Int("combined_score"), "native move-attack score and health weighting");
                if (combinedScore > 0) actual.Add([1, 2, checked((int)combinedScore)]);
            }
            var expected = v.GetProperty("expected").EnumerateArray().Select(row => row.EnumerateArray().Select(x => x.GetInt32()).ToArray()).ToArray();
            Check(actual.Count == expected.Length && actual.Zip(expected).All(p => p.First.SequenceEqual(p.Second)), "native positive tactical candidates and ordering");
        }
        var fixture = WorldChecks.Example();
        var actor = fixture.Units[1] with { Cell = 0, RemainingMovement = 6,
            Combatant = fixture.Units[1].Combatant with { Profile = fixture.Units[1].Combatant.Profile with { Grade = "0" } } };
        var enemy = fixture.Units[0] with { Cell = 2,
            Combatant = fixture.Units[0].Combatant with { Profile = fixture.Units[0].Combatant.Profile with { Grade = "0" } } };
        var scene = fixture with { Width = 3, Height = 1, Units = [actor, enemy], Cells = Enumerable.Range(0, 3).Select(i => new CellState { Owner = i == 0 ? actor.Owner : enemy.Owner }).ToArray() };
        var rules = new WorldAiRules { Tactics = LegacyAiTactics.Rules(), ExperienceLevels = new Dictionary<string, int> { ["0"] = 0 } };
        byte[] before = WorldSave.Encode(scene);
        var candidates = WorldAi.TacticalMoveCandidates(scene, actor.Id, rules);
        Check(candidates.Count == 2 && candidates[0].Action.Destination == 1 && candidates[0].Action.TargetId == null
            && candidates[1].Action.Destination == 1 && candidates[1].Action.TargetId == enemy.Id, "world AI generates capture and move-attack alternatives");
        Check(candidates[1].Score > candidates[0].Score && AiPlanning.Best(candidates) == 1, "world AI scores compound tactical action");
        Check(before.SequenceEqual(WorldSave.Encode(scene)), "tactical planning does not modify source state or RNG");
        Check(WorldAi.TacticalMoveCandidates(WorldSave.Decode(before, scene.ContentRevision), actor.Id, rules).SequenceEqual(candidates), "tactical plans deterministic after save/load");
        var moved = WorldAi.ExecuteMove(scene, candidates[0].Action, rules, new()).State;
        Check(moved.Units.Single(u => u.Id == actor.Id).Cell == 1 && moved.Cells[1].Owner == actor.Owner && !moved.Units.Single(u => u.Id == actor.Id).Acted,
            "AI capture command executes actual movement without spending attack");
        var combinedChange = WorldAi.ExecuteMove(scene, candidates[1].Action, rules, new());
        var combined = combinedChange.State;
        Check(combined.Revision == scene.Revision + 1 && combined.Units.Single(u => u.Id == actor.Id).Cell == 1 && combined.Units.Single(u => u.Id == actor.Id).Acted,
            "move-attack commits one command revision and both actions");
        Check(combinedChange.Notices.Any(n => n.Kind == "unit.moved") && combinedChange.Notices.Any(n => n.Kind == "battle.damage"), "compound command returns both movement and battle notices");
        var separate = WorldBattle.Attack(moved, moved.Revision, actor.Id, enemy.Id, new()).State with { Revision = combined.Revision };
        Check(WorldSave.Encode(separate).SequenceEqual(WorldSave.Encode(combined)), "atomic compound command matches separate legal movement/attack state");
        Reject(() => WorldAi.ExecuteMove(scene, candidates[1].Action with { TargetId = "missing" }, rules, new()), "attack.target");
        Check(before.SequenceEqual(WorldSave.Encode(scene)), "failed attack rolls back compound movement");
        Reject(() => WorldAi.ExecuteMove(combined, candidates[1].Action, rules, new()), "action.stale");
        var exhausted = scene with { Units = scene.Units.Select(u => u.Id != actor.Id ? u : u with { RemainingMovement = 0 }).ToArray() };
        Check(WorldAi.TacticalMoveCandidates(exhausted, actor.Id, rules).Count == 0, "no movement budget yields no tactical move candidates");
        var alreadyActed = scene with { Units = scene.Units.Select(u => u.Id != actor.Id ? u : u with { Acted = true }).ToArray() };
        Check(WorldAi.TacticalMoveCandidates(alreadyActed, actor.Id, rules).All(c => c.Action.TargetId == null), "acted mobile unit can reposition but cannot generate compound attack");
        var sea = scene with { Units = [actor with { Transport = true }, enemy with { Combatant = enemy.Combatant with { Profile = enemy.Combatant.Profile with { Category = "naval" } } }],
            Cells = scene.Cells.Select((c, i) => c with { Terrain = c.Terrain with { Sea = i != 0 } }).ToArray() };
        Check(WorldAi.TacticalMoveCandidates(sea, actor.Id, rules).All(c => c.Action.TargetId == null)
            && WorldAi.TacticalMoveCandidates(sea, actor.Id, rules with { UseOriginForMoveAttackScore = true }).Any(c => c.Action.TargetId == enemy.Id),
            "modern move-attack score uses destination sea state, with explicit original compatibility option");
        var factsBoundary = new AiMoveFacts(1, 1, AiTerritoryRelation.Hostile, Environment: 3);
        Check(AiMovement.MoveAttackScore(factsBoundary, 100, 35, 100, scoring) == 150
            && AiMovement.MoveAttackScore(factsBoundary, 100, 34, 100, scoring) == 51, "exact thirty-five-percent tactical health boundary");
        Check(AiMovement.MoveAttackScore(factsBoundary, 100, 100, 100, scoring with { PenalizeHazardsWhenAttacking = true }) == 75,
            "scenario can penalize hazardous firing positions");
        Reject(() => AiMovement.MoveScore(factsBoundary, scoring with { FullAttackWeightAtHealthFraction = 0 }), "ai.move_configuration");
        return checks;
    }
}
