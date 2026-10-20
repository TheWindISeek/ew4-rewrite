using Ew4.Core;
using Ew4.Persistence;

internal static class HazardDeathChecks
{
    public static int Run(string root)
    {
        int checks = 0;
        void Check(bool value, string label) { checks++; if (!value) throw new InvalidOperationException(label); }
        void Reject(Action action, string code)
        {
            try { action(); } catch (RuleViolation error) { Check(error.Code == code, code + ": " + error.Code); return; }
            throw new InvalidOperationException("Expected " + code);
        }
        var fixture = WorldChecks.Example();
        var player = fixture.Units[1] with { Cell = 0, Acted = true, RemainingMovement = 0, Burning = 1,
            Combatant = fixture.Units[1].Combatant with { Health = 1 } };
        var enemy = fixture.Units[0] with { Cell = 5, Acted = true };
        var world = fixture with { Width = 6, Height = 1, Units = [player, enemy],
            Cells = Enumerable.Range(0, 6).Select(i => new CellState { Owner = i == 5 ? enemy.Owner : player.Owner }).ToArray(),
            Factions = fixture.Factions.Select(f => f with { Survival = new(true, false, []), Resources = new(200, 100, 50), IncomeBonus = new(10, 5, 20) }).ToArray(),
            Events = [new() { Id = "lost", DialogueId = "casualty", Rules = [new() { Trigger = TriggerKind.UnitDestroyed, UnitId = player.Id }] }] };
        var fatal = new HazardRules { Nonlethal = false, Stages = new Dictionary<int, HazardStage>
            { [1] = new(10, [new(0, 100)]) } };
        var rules = new WorldTurnRules { VeterancyHealing = new Dictionary<string, int> { ["0"] = 0 },
            Settlement = new() { Burning = fatal, Environment = fatal } };
        var defaultEnded = WorldTurns.EndTurn(world, world.Revision, rules with { Settlement = new() }).State;
        Check(defaultEnded.Units.Any(u => u.Id == player.Id && u.Combatant.Health == 1), "native default hazards remain nonlethal");
        byte[] before = WorldSave.Encode(world);
        var death = WorldTurns.EndTurn(world, world.Revision, rules);
        var paused = death.State;
        Check(paused is { Phase: TurnPhase.EndFactionEvents, PendingDialogue: "casualty" } && paused.ActiveFaction == player.Owner
            && paused.Revision == world.Revision + 1 && paused.Units.All(u => u.Id != player.Id), "fatal hazard removes unit then pauses before advancing factions");
        Check(paused.Factions.Single(f => f.Id == player.Owner).Defeated && paused.Cells.Take(5).All(c => c.Owner == null),
            "hazard defeat releases territory without awarding a killer");
        Check(death.Notices.Count(n => n.Kind == "unit.destroyed") == 1 && death.Notices.Any(n => n.Kind == "hazard.unit_lost")
            && death.Notices.All(n => n.Kind is not "battle.kill" and not "battle.experience" and not "faction.started")
            && paused.Factions.All(f => f.Kills.Count == 0), "environment casualties do not grant combat rewards or begin next faction before dialogue");
        Check(before.SequenceEqual(WorldSave.Encode(world)), "fatal turn preserves original snapshot");
        Reject(() => WorldTurns.EndTurn(paused, paused.Revision, rules), "turn.phase");
        Reject(() => WorldTurns.Resume(paused, paused.Revision, rules), "action.dialogue_pending");
        var resumed = WorldTurns.Resume(paused, paused.Revision, rules, "casualty");
        var restored = WorldSave.Decode(WorldSave.Encode(paused), paused.ContentRevision);
        var replay = WorldTurns.Resume(restored, restored.Revision, rules, "casualty");
        Check(WorldSave.Encode(resumed.State).SequenceEqual(WorldSave.Encode(replay.State)) && resumed.Notices.SequenceEqual(replay.Notices),
            "ending-phase death dialogue resumes identically from save");
        Check(resumed.State is { Phase: TurnPhase.Active, Round: 2, PendingDialogue: null } && resumed.State.ActiveFaction == enemy.Owner
            && resumed.Notices.Count(n => n.Kind == "faction.started") == 1 && resumed.Notices.All(n => n.Kind != "unit.destroyed"),
            "resuming advances and starts next faction once without replaying death");
        var immediate = WorldTurns.EndTurn(world with { Events = [] }, world.Revision, rules).State;
        Check(immediate.ActiveFaction == enemy.Owner && immediate.Factions.Single(f => f.Id == player.Owner).Defeated,
            "fatal no-dialogue turn immediately advances past defeated faction");
        var leader = world with { Units = [player with { CommanderId = "leader" }, enemy],
            Commanders = [new() { Id = "leader", Owner = player.Owner }], Events = [] };
        var leaderDeath = WorldTurns.EndTurn(leader, leader.Revision, rules);
        Check(leaderDeath.State.Commanders.Single().Withdrawn && leaderDeath.Notices.Count(n => n.Kind == "commander.withdrawn") == 1,
            "hazards withdraw attached commanders through shared death settlement");
        var sea = leader with { Cells = leader.Cells.Select((c, i) => i == 0 ? c with { Terrain = c.Terrain with { Sea = true } } : c).ToArray(),
            Factions = leader.Factions.Select(f => f with { Survival = new(true, true, []) }).ToArray() };
        Check(WorldTurns.EndTurn(sea, sea.Revision, rules).State.Cells[0].Owner == null, "hazard deaths clear empty unbuilt sea ownership");

        var sibling = player with { Id = "sibling", Cell = 1 };
        var survivor = player with { Id = "survivor", Cell = 3, Burning = 0, Combatant = player.Combatant with { Health = 100 } };
        var queued = world with { Units = [player with { CommanderId = "aura", Support = new() { AdjacentHealing = 100 } }, sibling, survivor, enemy],
            Commanders = [new() { Id = "aura", Owner = player.Owner }], Events = [world.Events[0],
                new() { Id = "lost.sibling", DialogueId = "second.casualty", Rules = [new() { Trigger = TriggerKind.UnitDestroyed, UnitId = sibling.Id }] }] };
        var multi = WorldTurns.EndTurn(queued, queued.Revision, rules);
        Check(multi.State.Units.All(u => u.Id != player.Id && u.Id != sibling.Id) && multi.State.PendingDialogue == "casualty"
            && multi.State.QueuedDialogues.SequenceEqual(new[] { "second.casualty" }), "dead healing-aura commander cannot save a later neighbor and casualty dialogues queue");
        Check(!multi.State.Factions.Single(f => f.Id == player.Owner).Defeated && multi.State.TriggeredEvents.Count == 2,
            "surviving army prevents faction defeat after multiple hazard deaths");
        var afterFirst = WorldTurns.Resume(multi.State, multi.State.Revision, rules, "casualty");
        Check(afterFirst.State is { Phase: TurnPhase.EndFactionEvents, PendingDialogue: "second.casualty" } && afterFirst.State.Round == queued.Round
            && afterFirst.Notices.All(n => n.Kind != "faction.started"), "queued death dialogue keeps ending phase without starting a faction");
        var afterSecond = WorldTurns.Resume(WorldSave.Decode(WorldSave.Encode(afterFirst.State), queued.ContentRevision), afterFirst.State.Revision, rules, "second.casualty");
        Check(afterSecond.State.Phase == TurnPhase.Active && afterSecond.State.ActiveFaction == enemy.Owner
            && afterSecond.State.TriggeredEvents.Count == 2 && afterSecond.Notices.All(n => n.Kind != "unit.destroyed"),
            "last queued dialogue advances after restore without repeating settled hazards");
        var alone = world with { Factions = [world.Factions.Single(f => f.Id == player.Owner)], Units = [player],
            Cells = world.Cells.Select(c => c with { Owner = player.Owner }).ToArray(), Events = [] };
        Check(WorldTurns.EndTurn(alone, alone.Revision, rules).State is { Phase: TurnPhase.Finished, Outcome: BattleOutcome.Defeat },
            "no surviving factions terminates cleanly rather than starting a defeated faction");
        var invalid = world with { Units = [player, survivor, enemy],
            Cells = world.Cells.Select((c, i) => i == 4 ? c with { Environment = 9 } : c).ToArray() };
        byte[] invalidBefore = WorldSave.Encode(invalid);
        Reject(() => WorldTurns.EndTurn(invalid, invalid.Revision, rules), "settlement.site");
        Check(invalidBefore.SequenceEqual(WorldSave.Encode(invalid)), "late settlement failure rolls back earlier death and queued events");

        var spreadRules = rules with { Settlement = rules.Settlement with { Environment = fatal with
            { Stages = Enumerable.Range(1, 3).ToDictionary(i => i, _ => new HazardStage(10, [new(0, 100)])) } } };
        var spreading = world with { Units = [player, survivor, enemy], Events = [new()
            { Id = "death.fire", Rules = [new() { Trigger = TriggerKind.UnitDestroyed, UnitId = player.Id, Effect = EventEffectKind.EnvironmentSpread, Cell = 4 }] }] };
        var referenceRandom = new Ew4Random(spreading.RandomState);
        Hazards.EndUnit(1, 100, 1, referenceRandom, fatal);
        var eventSpread = WorldEvents.Spread(spreading with { RandomState = referenceRandom.State }, 4);
        referenceRandom = new Ew4Random(eventSpread.RandomState);
        foreach (int cell in Enumerable.Range(1, 4))
            Hazards.Decay(eventSpread.Cells[cell].Environment, referenceRandom, spreadRules.Settlement.Environment);
        var spreadEnd = WorldTurns.EndTurn(spreading, spreading.Revision, spreadRules);
        Check(spreadEnd.State.RandomState == referenceRandom.State && spreadEnd.State.TriggeredEvents.Contains("death.fire")
            && spreadEnd.State.Units.Single(u => u.Id == survivor.Id).Combatant.Health == (eventSpread.Cells[survivor.Cell].Environment == 0 ? 100 : 90)
            && spreadEnd.State.Cells[4].Environment == 0 && spreadEnd.State.Cells[5].Environment == eventSpread.Cells[5].Environment,
            "death event spreading and RNG persist into later site settlements without being overwritten by hazard RNG");

        var random = new Random(1930);
        for (int sample = 0; sample < 600; sample++)
        {
            int burning = random.Next(2), environment = random.Next(2), health = random.Next(1, 101);
            var source = world with { RandomState = (ulong)sample, Events = [], Units = [player with
                { Burning = burning, Combatant = player.Combatant with { Health = health } }, survivor, enemy],
                Cells = world.Cells.Select((c, i) => i == 0 ? c with { Environment = environment } : c).ToArray() };
            var pureRandom = new Ew4Random(source.RandomState);
            var pure = TurnSettlement.EndSite(new() { Environment = environment, Units = [new() { Id = player.Id,
                Health = health, MaximumHealth = 100, Burning = burning, Acted = true }] }, pureRandom, rules.Settlement);
            var changed = WorldTurns.EndTurn(source, source.Revision, rules);
            var result = changed.State.Units.SingleOrDefault(u => u.Id == player.Id);
            Check(result?.Combatant.Health == (pure.Units[0].Health == 0 ? null : pure.Units[0].Health)
                && changed.State.Cells[0].Environment == pure.Environment && changed.State.RandomState == pure.RandomState,
                "configured fatal world settlement agrees with pure damage and random state");
            Check(changed.Notices.Count(n => n.Kind == "unit.destroyed") == (result == null ? 1 : 0)
                && changed.State.AiRandomState == source.AiRandomState && changed.State.Revision == source.Revision + 1,
                "fatal hazard commits one death at most and no AI random draw");
            var saved = WorldSave.Decode(WorldSave.Encode(source), source.ContentRevision);
            Check(WorldSave.Encode(changed.State).SequenceEqual(WorldSave.Encode(WorldTurns.EndTurn(saved, saved.Revision, rules).State)),
                "random hazard world settles identically after restore");
        }
        return checks;
    }
}
