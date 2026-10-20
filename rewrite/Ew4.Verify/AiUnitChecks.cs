using System.Text.Json;
using Ew4.Core;
using Ew4.Persistence;

internal static class AiUnitChecks
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
        using var native = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "reverse/reports/ai_unit_dispatch_vectors.json")));
        foreach (var vector in native.RootElement.EnumerateArray())
        {
            var candidates = vector.GetProperty("candidates").EnumerateArray().Select(row => new AiCandidate<(int Destination, int Target)>(
                (row[0].GetInt32(), row[1].GetInt32()), row[2].GetInt64())).ToArray();
            int selected = AiPlanning.Best(candidates);
            var expectedIndex = vector.GetProperty("selected");
            if (expectedIndex.ValueKind == JsonValueKind.Null) Check(selected == -1, "native dispatch with no candidate requires fallback");
            else
            {
                Check(selected == expectedIndex.GetInt32(), "native stable maximum selection including ties");
                Check(candidates[selected].Action.Destination == vector.GetProperty("expected")[2].GetInt32()
                    && candidates[selected].Action.Target == vector.GetProperty("expected")[3].GetInt32(), "selected candidate matches original encoded destination and target");
            }
        }
        var fixture = WorldChecks.Example();
        var actor = fixture.Units[1] with { Cell = 0, RemainingMovement = 6, Behavior = "default",
            Combatant = fixture.Units[1].Combatant with { Profile = fixture.Units[1].Combatant.Profile with { Grade = "0" } } };
        var enemy = fixture.Units[0] with { Cell = 8,
            Combatant = fixture.Units[0].Combatant with { Profile = fixture.Units[0].Combatant.Profile with { Grade = "0" } } };
        var world = fixture with { Width = 9, Height = 1, Units = [actor, enemy], Cells = Enumerable.Range(0, 9).Select(i => new CellState
            { Owner = i == 8 ? enemy.Owner : actor.Owner, BuildingKind = i == 8 ? "0" : null, BuildingLevel = i == 8 ? 1 : 0 }).ToArray() };
        var rules = new WorldAiRules { Tactics = LegacyAiTactics.Rules(), ExperienceLevels = new Dictionary<string, int> { ["0"] = 0 } };
        var battle = new WorldBattleRules { ResolveIgnition = false, RecalculateLocalMorale = false };
        byte[] original = WorldSave.Encode(world);
        var plan = WorldAi.PlanUnit(world, actor.Id, rules);
        Check(plan.Candidates.Any(c => c.Action is AiHoldCommand) && plan.Candidates.Any(c => c.Action is AiStrategicCommand), "unified unit plan includes hold and strategic alternatives");
        Check(original.SequenceEqual(WorldSave.Encode(world)), "unified planning leaves world and RNGs unchanged");
        Check(plan.InitialRandomState != plan.FinalRandomState, "unified planning retains sampled strategic RNG state privately");
        var moved = WorldAi.ExecuteUnit(world, plan, rules, battle);
        Check(moved.Notices.Any(n => n.Kind == "unit.moved") && moved.State.AiRandomState == plan.FinalRandomState, "unified dispatcher executes best strategic candidate and commits AI RNG");
        Reject(() => WorldAi.ExecuteUnit(moved.State, plan, rules, battle), "action.stale");
        Reject(() => WorldAi.ExecuteUnit(world with { AiRandomState = world.AiRandomState + 1 }, plan, rules, battle), "ai.unit_stale_random");
        Reject(() => WorldAi.ExecuteUnit(world, plan, rules, battle, plan.Candidates.Count), "ai.unit_selection");
        var holdOnly = world with { Units = [actor with { Behavior = "3" }, enemy] };
        var holdPlan = WorldAi.PlanUnit(holdOnly, actor.Id, rules);
        var held = WorldAi.ExecuteUnit(holdOnly, holdPlan, rules, battle).State;
        Check(held.Units.Single(u => u.Id == actor.Id) is { RemainingMovement: 0, Acted: true }
            && held.RandomState == world.RandomState && held.AiRandomState == world.AiRandomState, "hold ends both remaining unit actions without sampling randomness");
        Check(WorldAi.PlanNextUnit(held, rules) == null, "held unit cannot make scheduler loop forever");
        var building = world with { Units = [actor with { RemainingMovement = 0, Combatant = actor.Combatant with { ConstructionTurns = 2 } }, enemy] };
        Check(WorldAi.PlanNextUnit(building, rules) == null, "immobile unfinished unit skipped by scheduler");
        var spent = world with { Units = [actor with { RemainingMovement = 0, Acted = true }, enemy] };
        Check(WorldAi.PlanNextUnit(spent, rules) == null, "unit with no remaining action skipped");
        var unmapped = world with { Units = [actor with { Behavior = "unknown" }, enemy] };
        Reject(() => WorldAi.PlanNextUnit(unmapped, rules), "ai.behavior");

        var longRangeActor = actor with { Combatant = actor.Combatant with { Profile = actor.Combatant.Profile with { MaximumRange = 8 } } };
        var ranged = world with { Units = [longRangeActor, enemy], Cells = world.Cells.Select((c, i) => i != 7 ? c : c with
            { Owner = enemy.Owner, BuildingKind = "0", BuildingLevel = 1 }).ToArray() };
        var rangedPlan = WorldAi.PlanUnit(ranged, actor.Id, rules);
        Check(rangedPlan.Candidates.Any(c => c.Action is AiStrategicCommand) && rangedPlan.Candidates[AiPlanning.Best(rangedPlan.Candidates)].Action is AiStrikeCommand,
            "unified scheduler compares strategic movement against legal immediate attack");
        var struck = WorldAi.ExecuteUnit(ranged, rangedPlan, rules, battle);
        Check(struck.Notices.Any(n => n.Kind == "battle.damage") && struck.State.RandomState != ranged.RandomState
            && struck.State.AiRandomState == rangedPlan.FinalRandomState && rangedPlan.FinalRandomState != ranged.AiRandomState,
            "attack commits combat RNG and discarded strategic alternative's sampled AI RNG independently");
        var strike = (AiStrikeCommand)rangedPlan.Candidates[AiPlanning.Best(rangedPlan.Candidates)].Action;
        var separately = WorldAi.ExecuteStrike(ranged, strike, battle).State with { AiRandomState = rangedPlan.FinalRandomState };
        Check(WorldSave.Encode(struck.State).SequenceEqual(WorldSave.Encode(separately)), "unified attack state matches public world attack exactly");
        var tactical = world with { Width = 3, Height = 1, Units = [actor with { Behavior = "1" }, enemy with { Cell = 2 }],
            Cells = Enumerable.Range(0, 3).Select(i => new CellState { Owner = i == 0 ? actor.Owner : enemy.Owner }).ToArray() };
        var tacticalPlan = WorldAi.PlanUnit(tactical, actor.Id, rules);
        Check(tacticalPlan.Candidates[AiPlanning.Best(tacticalPlan.Candidates)].Action is AiMoveCommand { TargetId: not null }, "unified scheduler chooses superior compound movement/attack");
        var compound = WorldAi.ExecuteUnit(tactical, tacticalPlan, rules, battle);
        Check(compound.State.Revision == tactical.Revision + 1 && compound.Notices.Any(n => n.Kind == "unit.moved") && compound.Notices.Any(n => n.Kind == "battle.damage"),
            "unified compound action keeps one revision and all notices");
        var tampered = tacticalPlan with { Candidates = [new(new AiMoveCommand(world.Revision, actor.Id, 1, "missing"), 999)] };
        byte[] tacticalBefore = WorldSave.Encode(tactical);
        Reject(() => WorldAi.ExecuteUnit(tactical, tampered, rules, battle), "attack.target");
        Check(tacticalBefore.SequenceEqual(WorldSave.Encode(tactical)), "unified failed compound command leaves movement and both RNGs untouched");

        var actors = new[] { actor with { Cell = 0 }, actor with { Id = "second", Cell = 7, Behavior = "2" }, enemy with { Cell = 20, Behavior = "default", RemainingMovement = 6 } };
        var campaign = fixture with { Width = 7, Height = 3, Units = actors, Cells = Enumerable.Range(0, 21).Select(i => new CellState
            { Owner = i == 20 ? enemy.Owner : actor.Owner, BuildingKind = i == 20 ? "0" : null, BuildingLevel = i == 20 ? 1 : 0 }).ToArray(),
            Factions = fixture.Factions.Select(f => f with { Human = false }).ToArray() };
        var turn = new WorldTurnRules { Buildings = [new("0", 1, new(0, 0, 0))], VeterancyHealing = new Dictionary<string, int> { ["0"] = 0 } };
        var uninterrupted = campaign;
        var resumed = WorldSave.Decode(WorldSave.Encode(campaign), campaign.ContentRevision);
        bool attacked = false, advanced = false;
        for (int factionTurn = 0; factionTurn < 8; factionTurn++)
        {
            int steps = 0;
            while (WorldAi.PlanNextUnit(uninterrupted, rules) is { } next)
            {
                if (++steps > 20) throw new InvalidOperationException("AI unit scheduler did not exhaust actions");
                var restoredNext = WorldAi.PlanNextUnit(resumed, rules)!;
                Check(next.UnitId == restoredNext.UnitId && next.Candidates.SequenceEqual(restoredNext.Candidates)
                    && next.FinalRandomState == restoredNext.FinalRandomState, "continuous and checkpoint-restored AI choose identical plans");
                var change = WorldAi.ExecuteUnit(uninterrupted, next, rules, battle);
                uninterrupted = change.State;
                resumed = WorldAi.ExecuteUnit(resumed, restoredNext, rules, battle).State;
                attacked |= change.Notices.Any(n => n.Kind == "battle.damage");
                advanced |= change.Notices.Any(n => n.Kind == "unit.moved");
                Check(WorldSave.Encode(uninterrupted).SequenceEqual(WorldSave.Encode(resumed)), "AI action matches after restoring every previous checkpoint");
                resumed = WorldSave.Decode(WorldSave.Encode(resumed), resumed.ContentRevision);
            }
            Check(WorldAi.PlanNextUnit(resumed, rules) == null, "resumed AI faction also exhausts unit actions");
            uninterrupted = WorldTurns.EndTurn(uninterrupted, uninterrupted.Revision, turn).State;
            resumed = WorldTurns.EndTurn(resumed, resumed.Revision, turn).State;
            Check(WorldSave.Encode(uninterrupted).SequenceEqual(WorldSave.Encode(resumed)), "AI scheduling and turn refresh agree across round boundaries");
        }
        Check(attacked && advanced && uninterrupted.Round > campaign.Round, "multiple AI factions move/fight/finish turns across several rounds");
        var ordered = campaign with { Units = campaign.Units.Reverse().ToArray() };
        Check(WorldAi.PlanNextUnit(campaign, rules)!.UnitId == WorldAi.PlanNextUnit(ordered, rules)!.UnitId, "unit scheduler stable by cell and ID rather than collection order");
        Check(AiPlanning.HoldScore(new(5_000_000_000, 1, 2, 0, false, false, false, 100, 100)) == 10_000_000_000,
            "unified hold scoring has no original 32-bit value cap");
        return checks;
    }
}
