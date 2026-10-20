using System.Text.Json;
using Ew4.Core;
using Ew4.Persistence;

internal static class BattleCommitChecks
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
            try { action(); } catch (RuleViolation error) { Check(error.Code == code, code + " != " + error.Code); return; }
            throw new InvalidOperationException("Expected " + code);
        }
        var fixture = WorldChecks.Example();
        var profile = fixture.Units[0].Combatant.Profile;
        string[] categories = ["infantry", "cavalry", "artillery", "naval", "fort"];
        var isolated = new WorldBattleRules { CheckFactionSurvival = false, ResolveIgnition = false,
            RecalculateLocalMorale = false, KillDialogueProbability = 0 };
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "reverse/reports/battle_commit_vectors.json")));
        int sample = 0;
        foreach (var vector in data.RootElement.EnumerateArray())
        {
            var health = vector.GetProperty("health");
            var morale = vector.GetProperty("morale");
            var units = Enumerable.Range(0, 3).Select(i => new UnitState
            {
                Id = "unit." + i, Owner = i == 0 ? "winner" : "loser", Cell = i, RemainingMovement = 6,
                Morale = new(morale[i].GetInt32(), 0, morale[i].GetInt32()),
                Combatant = new() { Health = health[i].GetInt32(), Morale = morale[i].GetInt32(), Profile = profile with
                    { Id = "legacy.unit." + (10 + i), Category = categories[vector.GetProperty("categories")[i].GetInt32()],
                        Grade = i.ToString(), MaxHealth = 150, KillExperience = i + 1 } }
            }).ToArray();
            var state = fixture with { Width = 3, Height = 1, Units = units,
                Cells = units.Select(u => new CellState { Owner = u.Owner, InstallationKind = "trench" }).ToArray(),
                RandomState = vector.GetProperty("seed").GetUInt64() };
            var battle = new ResolvedBattle("unit.0", "unit.1", vector.GetProperty("taken").GetInt32(),
                vector.GetProperty("dealt").GetInt32(), vector.GetProperty("counter").GetBoolean(), state.RandomState,
                vector.GetProperty("splash").GetInt32() == 0 ? null : "unit.2", vector.GetProperty("splash").GetInt32());
            var change = WorldBattle.CommitResolved(state, battle, isolated);
            var expected = vector.GetProperty("expected");
            for (int i = 0; i < units.Length; i++)
            {
                var unit = change.State.Units.SingleOrDefault(u => u.Id == units[i].Id);
                int hp = expected.GetProperty("health")[i].GetInt32();
                Check((unit?.Combatant.Health ?? 0) == hp, "native battle HP/death " + sample);
                if (unit != null)
                {
                    var wanted = expected.GetProperty("morale")[i];
                    Check(unit.Morale == new MoraleState(wanted[0].GetInt32(), wanted[1].GetInt32(), wanted[2].GetInt32())
                        && unit.Acted == expected.GetProperty("acted")[i].GetBoolean()
                        && unit.RemainingMovement == expected.GetProperty("movement")[i].GetInt32(), "native battle morale/action " + sample);
                }
            }
            var damageNotices = change.Notices.Where(n => n.Kind == "battle.damage").Select(n => (n.Subject, n.Value));
            var damageTrace = expected.GetProperty("trace").EnumerateArray().Where(t => t[0].GetString() == "damage").Select(t => ("unit." + t[1].GetInt32(), t[2].GetInt64()));
            Check(damageNotices.SequenceEqual(damageTrace), "native battle damage order " + sample);
            var gainKinds = new Dictionary<string, string> { ["experience"] = "battle.experience", ["rank_experience"] = "battle.rank_experience", ["nobility_experience"] = "battle.title_experience" };
            var gained = change.Notices.Where(n => gainKinds.Values.Contains(n.Kind)).Select(n => (n.Kind, n.Subject, n.Value));
            var nativeGains = expected.GetProperty("trace").EnumerateArray().Where(t => gainKinds.ContainsKey(t[0].GetString()!))
                .Select(t => (gainKinds[t[0].GetString()!], "unit." + t[1].GetInt32(), t[2].GetInt64()));
            Check(gained.SequenceEqual(nativeGains), "native battle XP ordering " + sample);
            Check(change.State.RandomState == expected.GetProperty("rng_state").GetUInt64(), "native battle random state " + sample);
            Check(change.State.Revision == state.Revision + 1 && change.State.AiRandomState == state.AiRandomState, "battle revision and independent AI RNG");
            sample++;
        }

        var attacker = fixture.Units.Single(u => u.Owner == "winner") with { Cell = 1, RemainingMovement = 6 };
        var defender = fixture.Units.Single(u => u.Owner == "loser") with { Cell = 0, RemainingMovement = 6 };
        var initial = fixture with { Width = 3, Height = 1, Units = [attacker, defender],
            Cells = [new() { Owner = "loser", InstallationKind = "bunker" }, new() { Owner = "winner" }, new()],
            Factions = fixture.Factions.Select(f => f with { Resources = new(100, 200, 300) }).ToArray() };
        byte[] before = WorldSave.Encode(initial);
        var publicRules = new WorldBattleRules { Context = new() { InstallationDefense = new Dictionary<string, IReadOnlyDictionary<string, int>>
            { ["bunker"] = categories.ToDictionary(c => c, _ => 10) } } };
        var legal = WorldBattle.Attack(initial, 0, attacker.Id, defender.Id, publicRules);
        Check(legal.State.Revision == 1 && legal.Notices.Any(n => n.Kind == "battle.damage"), "legal public attack calculates and commits");
        Check(before.SequenceEqual(WorldSave.Encode(initial)), "successful attack detaches input state");
        Check(legal.State.Factions.All(f => f.Resources == new Resources(100, 200, 300)), "ordinary attack does not invent resource loot");
        Reject(() => WorldBattle.Attack(initial, 99, attacker.Id, defender.Id, new()), "action.stale");
        Reject(() => WorldBattle.Attack(initial, 0, defender.Id, attacker.Id, new()), "attack.target");
        Reject(() => WorldBattle.Attack(initial with { Units = [attacker with { Acted = true }, defender] }, 0, attacker.Id, defender.Id, new()), "attack.unavailable");
        Reject(() => WorldBattle.Attack(initial with { Units = [attacker with { Cell = 2 }, defender] }, 0, attacker.Id, defender.Id, new()), "attack.unavailable");
        var lethal = initial with { Units = [attacker, defender with { Combatant = defender.Combatant with { Health = 1 } }] };
        var victory = WorldBattle.Attack(lethal, 0, attacker.Id, defender.Id, publicRules with { PlayerCamp = "b", InstallationDestructionProbability = 100 }).State;
        Check(victory.Factions.Single(f => f.Id == "loser").Defeated && victory.Cells[0].Owner == "winner", "last-unit death triggers surrender and land transfer");
        Check(victory.Phase == TurnPhase.Finished && victory.Outcome == BattleOutcome.Victory && victory.Cells[0].InstallationKind == null, "victory and configurable installation destruction");
        Reject(() => WorldBattle.Attack(victory, victory.Revision, attacker.Id, defender.Id, new()), "action.phase");

        var general = new CommanderRuntime { Id = "commander", Owner = "winner" };
        var experienced = initial with { Commanders = [general], Units = [attacker with
        {
            CommanderId = general.Id, Combatant = attacker.Combatant with { Health = 50 }
        }, defender with { Combatant = defender.Combatant with { Health = 12, Profile = defender.Combatant.Profile with { KillExperience = 3 } } }] };
        var experienceRules = isolated with
        {
            UnitProgression = new() { Levels = [new("0", 10), new("1", null, HealingOnEntry: 20)], SingleAdvancePerAward = true, AccumulateAtCap = true, DiscardExperienceOnReachingCap = false },
            RankProgression = new() { Levels = [new("0", 40), new("officer", null)] },
            TitleProgression = new() { Levels = [new("0", 2), new("veteran", null)] },
            RankHealthBonuses = new Dictionary<string, int> { ["officer"] = 20 }, TitleHealing = new Dictionary<string, int> { ["veteran"] = 7 }
        };
        var promoted = WorldBattle.CommitResolved(experienced, new(attacker.Id, defender.Id, 10, 20, true, 1946), experienceRules).State;
        var promotedUnit = promoted.Units.Single();
        Check(promotedUnit.Veterancy == "1" && promotedUnit.Experience == 5 && promotedUnit.Combatant.Health == 72 && promotedUnit.Combatant.Profile.MaxHealth == 120,
            "unit healing precedes commander rank HP scaling");
        Check(promoted.Commanders.Single().Rank.Level == 1 && promoted.Commanders.Single().Title.Level == 1 && promotedUnit.Support.TitleHealing == 7,
            "rank and title tracks update persistent commander and resolved bonuses");
        Check(WorldSave.Encode(promoted).SequenceEqual(WorldSave.Encode(WorldSave.Decode(WorldSave.Encode(promoted), "fixture.1"))), "commander progress survives save/load");
        var invalidTrack = experienced with { Units = experienced.Units.Select(u => u.Id == attacker.Id ? u with { Veterancy = "missing" } : u).ToArray() };
        byte[] invalidBefore = WorldSave.Encode(invalidTrack);
        Reject(() => WorldBattle.CommitResolved(invalidTrack, new(attacker.Id, defender.Id, 1, 20, true, 123), experienceRules), "progression.state");
        Check(invalidBefore.SequenceEqual(WorldSave.Encode(invalidTrack)), "failed XP commit preserves units/commander/installation/RNG");

        var simultaneous = initial with { Commanders = [general, new() { Id = "enemy.commander", Owner = "loser" }],
            Units = [attacker with { CommanderId = general.Id, Combatant = attacker.Combatant with { Health = 1 } },
                defender with { CommanderId = "enemy.commander", Combatant = defender.Combatant with { Health = 1 } },
                defender with { Id = "splash", Cell = 2, Combatant = defender.Combatant with { Health = 1 } }],
            Events = [new() { Id = "a", DialogueId = "a.dialogue", Rules = [new() { Trigger = TriggerKind.UnitDestroyed, UnitId = attacker.Id }] },
                new() { Id = "d", DialogueId = "d.dialogue", Rules = [new() { Trigger = TriggerKind.UnitDestroyed, UnitId = defender.Id, Effect = EventEffectKind.EnvironmentSpread, Cell = 0 }] },
                new() { Id = "s", DialogueId = "s.dialogue", Rules = [new() { Trigger = TriggerKind.UnitDestroyed, UnitId = "splash" }] }] };
        var dead = WorldBattle.CommitResolved(simultaneous, new(attacker.Id, defender.Id, 2, 2, true, 1946, "splash", 2), isolated).State;
        Check(dead.Units.Count == 0 && dead.Commanders.All(c => c.Withdrawn) && dead.TriggeredEvents.Count == 3, "simultaneous and splash deaths process all events and withdrawals");
        Check(dead.PendingDialogue == "a.dialogue" && dead.QueuedDialogues.SequenceEqual(["d.dialogue", "s.dialogue"]) && dead.Cells[0].Environment == 3,
            "death effects commit in order and queue every dialogue");
        var cursor = WorldSave.Decode(WorldSave.Encode(dead), "fixture.1");
        foreach (string dialogue in new[] { "a.dialogue", "d.dialogue", "s.dialogue" })
        {
            Check(cursor.PendingDialogue == dialogue, "death dialogue continuation order");
            cursor = WorldSave.Decode(WorldSave.Encode(WorldEvents.Acknowledge(cursor, dialogue)), "fixture.1");
        }
        Check(cursor.PendingDialogue == null && cursor.QueuedDialogues.Count == 0, "acknowledgements drain death dialogues without replaying combat");
        Reject(() => WorldBattle.Attack(dead, dead.Revision, attacker.Id, defender.Id, new()), "action.dialogue_pending");

        var seaDeath = initial with { Cells = initial.Cells.Select((c, i) => i == 0 ? c with { Terrain = c.Terrain with { Sea = true } } : c).ToArray(),
            Units = [attacker, defender with { Combatant = defender.Combatant with { Health = 1 } }] };
        Check(WorldBattle.CommitResolved(seaDeath, new(attacker.Id, defender.Id, 0, 10, false, 1946), isolated).State.Cells[0].Owner == null,
            "dead sea occupant releases empty sea ownership");
        var incendiary = initial with { Units = [attacker with { Combatant = attacker.Combatant with { Profile = attacker.Combatant.Profile with { IgnitionNumerator = 1 } } }, defender] };
        var alwaysBurn = isolated with { ResolveIgnition = true, TerrainFireWeights = new Dictionary<string, IReadOnlyList<int>> { ["open"] = new[] { 100 } } };
        Check(WorldBattle.CommitResolved(incendiary, new(attacker.Id, defender.Id, 0, 1, false, 1946), alwaysBurn).State.Cells[0].Environment == 1,
            "resolved attack ignition affects actual world terrain");
        var immune = incendiary with { Units = [incendiary.Units[0], defender with { Combatant = defender.Combatant with { FireImmune = true } }] };
        Check(WorldBattle.CommitResolved(immune, new(attacker.Id, defender.Id, 0, 1, false, 1946), alwaysBurn).State.Cells[0].Environment == 0,
            "fire immunity blocks ignition before RNG");
        var bark = WorldBattle.CommitResolved(experienced, new(attacker.Id, defender.Id, 0, 50, false, 1946), isolated with { KillDialogueProbability = 100 });
        Check(bark.Notices.Any(n => n.Kind == "battle.kill_dialogue") && bark.State.Factions.All(f => f.Resources == new Resources(100, 200, 300)),
            "kill dialogue never awards fictitious loot");
        var missile = attacker with { Combatant = attacker.Combatant with { Profile = attacker.Combatant.Profile with
            { Traits = ["consumed_on_attack"] } } };
        var noCounter = defender with { Combatant = defender.Combatant with { Profile = defender.Combatant.Profile with
            { Traits = ["no_retaliation"] } } };
        var missileWorld = initial with { Units = [missile, noCounter] };
        var weaponRules = isolated with { Context = publicRules.Context };
        var savedMissile = WorldSave.Decode(WorldSave.Encode(missileWorld), "fixture.1");
        Check(savedMissile.Units.Single(u => u.Id == missile.Id).Combatant.Profile.HasTrait("consumed_on_attack"),
            "single-use weapon trait survives save/load before firing");
        var consumed = WorldBattle.Attack(savedMissile, savedMissile.Revision, missile.Id, noCounter.Id, weaponRules);
        Check(consumed.State.Units.All(u => u.Id != missile.Id) && consumed.State.Units.Single().Combatant.Health < noCounter.Combatant.Health,
            "single-use attacker deals backend damage before removal even without retaliation");
        Check(consumed.Notices.Count(n => n.Kind == "unit.destroyed" && n.Subject == missile.Id) == 1
            && consumed.Notices.Any(n => n.Kind == "unit.consumed" && n.Subject == missile.Id)
            && consumed.State.Factions.All(f => f.Kills.Count == 0), "consumption removes once without giving the enemy a kill");
        Check(WorldSave.Decode(WorldSave.Encode(consumed.State), "fixture.1").Units.All(u => u.Id != missile.Id),
            "consumed weapon remains absent after save/load");
        Check(missileWorld.Units.Count == 2, "weapon consumption leaves caller state intact");
        var missileDefender = defender with { Combatant = defender.Combatant with { Profile = defender.Combatant.Profile with
            { Traits = ["consumed_on_attack"] } } };
        var retaliating = initial with { Units = [attacker, missileDefender] };
        var retaliation = WorldBattle.Attack(retaliating, 0, attacker.Id, defender.Id, weaponRules);
        Check(retaliation.State.Units.All(u => u.Id != defender.Id)
            && retaliation.Notices.Any(n => n.Kind == "unit.consumed" && n.Subject == defender.Id), "actual retaliation consumes defender weapon");
        var outOfRange = missileDefender with { Combatant = missileDefender.Combatant with { Profile = missileDefender.Combatant.Profile with
            { MinimumRange = 2, MaximumRange = 3 } } };
        var notFired = WorldBattle.Attack(initial with { Units = [attacker, outOfRange] }, 0, attacker.Id, defender.Id, weaponRules);
        Check(notFired.State.Units.Any(u => u.Id == defender.Id) && !notFired.Notices.Any(n => n.Kind == "unit.consumed"),
            "weapon not firing a retaliation remains available");
        var lethalMissile = missileDefender with { Combatant = missileDefender.Combatant with { Health = 1 } };
        var destroyedBeforeFiring = WorldBattle.Attack(initial with { Units = [attacker, lethalMissile] }, 0, attacker.Id, defender.Id,
            weaponRules with { Combat = new() { LethalStrikePreventsCounterattack = true } });
        Check(!destroyedBeforeFiring.Notices.Any(n => n.Kind == "unit.consumed")
            && destroyedBeforeFiring.Notices.Count(n => n.Kind == "unit.destroyed" && n.Subject == defender.Id) == 1,
            "lethal attack prevents retaliation and duplicate consumption");
        Reject(() => WorldBattle.Attack(consumed.State, consumed.State.Revision, missile.Id, defender.Id, weaponRules), "attack.target");
        return checks;
    }
}
