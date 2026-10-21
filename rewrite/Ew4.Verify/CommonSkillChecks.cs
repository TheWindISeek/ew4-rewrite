using System.Text;
using System.Text.Json;
using Ew4.Client;
using Ew4.Core;
using Ew4.Legacy;
using Ew4.Persistence;

internal static class CommonSkillChecks
{
    public static int Run(string root)
    {
        int checks = 0;
        void Check(bool pass, string label) { if (!pass) throw new Exception("Common skills: " + label); checks++; }
        void Reject(Action action, string label)
        {
            try { action(); } catch (Exception e) when (e is RuleViolation or JsonException) { checks++; return; }
            throw new Exception("Common skills accepted " + label);
        }
        string example = Path.Combine(root, "rewrite/mod-kit/examples/common-skills");
        var files = Directory.EnumerateFiles(example, "*", SearchOption.AllDirectories).Where(p => !p.EndsWith(".md"))
            .ToDictionary(p => Path.GetRelativePath(example, p).Replace('\\', '/'), File.ReadAllBytes, StringComparer.Ordinal);
        byte[] Read(string path) => files.TryGetValue(path, out var bytes) ? bytes : throw new FileNotFoundException(path);
        var loaded = ExtensionLoader.Load(Read);
        var bus = loaded.Bus;
        Check(loaded.ApiVersion == 2 && bus.Skills.Count == 6 && bus.Statuses.Count == 2, "external API 2 definitions and Lua load");
        var fixture = WorldChecks.Example();
        var profile = fixture.Units[0].Combatant.Profile;
        bus.RegisterUnitTemplates([new(profile with { Id = "ew4.unit.fra.line_infantry.0" }, new() { FoodConsumption = 2 })]);
        var map = new HexMap(7, 7);
        var caster = fixture.Units[1] with { Cell = 24, Combatant = fixture.Units[1].Combatant with {
            SkillSlots = bus.Skills.Values.Select(s => new SkillSlot(s.Id, "unit." + s.Id.Split('.').Last(), s.Id == "example.blast" ? 2 : 1)).ToArray() } };
        var enemy = fixture.Units[0] with { Cell = 25 };
        var ally = fixture.Units[1] with { Id = "ally.second", Cell = map.Neighbor(caster.Cell, 3) };
        var world = fixture with { Width = 7, Height = 7,
            Cells = Enumerable.Range(0, 49).Select(i => new CellState { Owner = i == 0 ? "loser" : "winner" }).ToArray(),
            Factions = fixture.Factions.Select(f => f with { Resources = new(100, 100, 100) }).ToArray(),
            Units = [caster, enemy, enemy with { Id = "enemy.second", Cell = map.Neighbor(enemy.Cell, 0) }, ally, enemy with { Id = "enemy.reserve", Cell = 0 }] };
        world.Validate();
        var move = new WorldMoveRules { Extensions = bus };
        var turns = new WorldTurnRules { Extensions = bus };
        WorldChange Cast(WorldState w, string name, string? target = null, int? cell = null) =>
            WorldSkillActions.Execute(w, w.Revision, caster.Id, "unit." + name, target, bus, move, cell);
        UnitState Unit(WorldState w, string id) => w.Units.Single(u => u.Id == id);
        WorldState Rearm(WorldState w) => w with { Round = w.Round + 3, ActiveFaction = caster.Owner,
            Units = w.Units.Select(u => u.Id == caster.Id ? u with { Acted = false } : u).ToArray() };
        var initialBytes = WorldSave.Encode(world);

        var summon = Cast(world, "summon", cell: 17);
        var added = summon.State.Units.Single(u => world.Units.All(old => old.Id != u.Id));
        Check(added.Cell == 17 && added.Owner == caster.Owner && added.Combatant.Profile.Id == "ew4.unit.fra.line_infantry.0", "summon template and selected cell");
        Check(added.Acted && added.RemainingMovement == 0 && added.Support.FoodConsumption == 2, "summon support and initial action");
        Check(summon.State.Factions.Single(f => f.Id == caster.Owner).Resources.Money == 95 && summon.State.Revision == 1, "one summon cost and revision");
        Check(summon.Notices.Any(n => n.Kind == "unit.recruited" && n.Subject == added.Id), "summon notification");
        Check(WorldSave.Encode(summon.State).SequenceEqual(WorldSave.Encode(Cast(world, "summon", cell: 17).State)), "deterministic summon IDs");
        Check(Unit(WorldSave.Decode(WorldSave.Encode(summon.State), world.ContentRevision), added.Id).Support.FoodConsumption == 2, "summon reload");
        Reject(() => Cast(world, "summon"), "missing cell");
        Reject(() => Cast(world, "summon", cell: 0), "enemy owned destination");
        Reject(() => Cast(world, "summon", cell: 25), "occupied destination");
        Reject(() => Cast(world, "summon", target: enemy.Id, cell: 17), "ambiguous target");
        Reject(() => Cast(world with { Limits = new() { UnitsPerFaction = 2 } }, "summon", cell: 17), "faction capacity");
        Reject(() => Cast(world with { Cells = world.Cells.Select((c, i) => i == 17 ? c with { Terrain = c.Terrain with { Sea = true } } : c).ToArray() }, "summon", cell: 17), "land summon at sea");
        Reject(() => Cast(world, "summon", cell: -1), "negative cell");
        Reject(() => Cast(world, "summon", cell: 1000), "out of bounds");
        Reject(() => Cast(world with { Units = world.Units.Select(u => u.Id == caster.Id ? u with { Cell = 48 } : u).ToArray() }, "summon", cell: 1), "out of range");

        var teleport = Cast(world, "teleport", cell: 17);
        Check(Unit(teleport.State, caster.Id).Cell == 17 && Unit(teleport.State, caster.Id).Acted, "teleport moves caster and ends action");
        Check(world.Cells.Select(c => c.Owner).SequenceEqual(teleport.State.Cells.Select(c => c.Owner)), "teleport does not claim territory");
        Check(teleport.Notices.Any(n => n.Kind == "unit.moved"), "teleport notification");
        Reject(() => Cast(world, "teleport", cell: caster.Cell), "teleport to origin");
        Reject(() => Cast(world, "teleport", cell: enemy.Cell), "teleport to occupied cell");
        var arrival = new ExtensionSkill { Id = "test.arrival", Effects = [new() { Event = "unit.move_end", Operation = "heal", Value = 5 }] };
        var arrivalBus = new RuleEventBus(bus.Skills.Values.Append(arrival).ToDictionary(s => s.Id), bus.Scripts, bus.Statuses);
        var wounded = world with { Units = world.Units.Select(u => u.Id == caster.Id ? u with { Combatant = u.Combatant with {
            Health = 60, SkillSlots = u.Combatant.SkillSlots.Append(new("test.arrival", "arrival")).ToArray() } } : u).ToArray() };
        Check(Unit(WorldSkillActions.Execute(wounded, 0, caster.Id, "unit.teleport", null, arrivalBus, move, 17).State, caster.Id).Combatant.Health == 65, "teleport move-end event");

        var blast = Cast(world, "blast", cell: enemy.Cell);
        var affected = world.Units.Where(u => u.Owner != caster.Owner && map.Distance(enemy.Cell, u.Cell) <= 1).ToArray();
        Check(affected.Length == 2 && affected.All(u => Unit(blast.State, u.Id).Combatant.Health == 70), "area Lua per enemy");
        Check(Unit(blast.State, caster.Id).Combatant.Health == 100 && Unit(blast.State, ally.Id).Combatant.Health == 100, "area excludes allies");
        Check(blast.State.ExtensionState[caster.Id + "/unit.blast/hits"] == 2, "area Lua cumulative state");
        Check(blast.State.Factions.Single(f => f.Id == caster.Owner).Resources == new Resources(96, 98, 100), "area charges once");
        Check(blast.State.ExtensionState[caster.Id + "/unit.blast/ready"] == 3 && blast.State.Revision == 1, "area cooldown once");
        Check(WorldSave.Encode(blast.State).SequenceEqual(WorldSave.Encode(Cast(world, "blast", cell: enemy.Cell).State)), "area deterministic replay");
        var lethalWorld = world with { Units = world.Units.Select(u => affected.Any(a => a.Id == u.Id)
            ? u with { Combatant = u.Combatant with { Health = 1 } } : u).ToArray() };
        var lethal = Cast(lethalWorld, "blast", cell: enemy.Cell);
        Check(affected.All(u => lethal.State.DestroyedScenarioUnits.Contains(u.Id)) && lethal.Notices.Count(n => n.Kind == "unit.destroyed") == 2, "area unified deaths");
        var neutral = world with { DiplomaticRelations = [new("loser", "winner", "neutral")] };
        Check(WorldDiplomacy.Relation(Cast(neutral, "blast", cell: enemy.Cell).State, "winner", "loser") == "hostile", "area neutral provocation");

        var rally = Cast(world, "rally").State;
        Check(Unit(rally, caster.Id).Combatant.Statuses.Single().RemainingTurns == 2 && Unit(rally, ally.Id).Combatant.Statuses.Count == 1, "area status on allies");
        Check(Unit(rally, enemy.Id).Combatant.Statuses.Count == 0, "status excludes enemies");
        Check(bus.MovementBudget(Unit(rally, caster.Id).Combatant) == 9 && Unit(rally, caster.Id).RemainingMovement == 3, "movement limit and immediate restore");
        rally.Validate();
        Check(WorldActions.Reachable(rally, caster.Id, move).Count > 1, "buffed path search");
        var saved = WorldSave.Decode(WorldSave.Encode(rally), world.ContentRevision);
        Check(Unit(saved, caster.Id).Combatant.Statuses.Single().Id == "example.vigor" && Unit(saved, caster.Id).ExtensionMovementLimit == 9, "status reload");
        Check(!ReferenceEquals(rally.Copy().Units[0].Combatant.Statuses, rally.Units[0].Combatant.Statuses), "snapshot copies status arrays");
        Check(bus.Modify("combat.modify_damage", "damage_multiplier", 100, Unit(saved, caster.Id).Combatant, enemy.Combatant, new(1)) == 120, "offensive status");
        Check(bus.Modify("combat.modify_incoming_damage", "damage_multiplier", 100, Unit(saved, caster.Id).Combatant, enemy.Combatant, new(1)) == 80, "defensive status");
        var nextEnemy = WorldTurns.EndTurn(saved, saved.Revision, turns).State;
        Check(Unit(nextEnemy, caster.Id).Combatant.Statuses.Single().RemainingTurns == 1, "status duration counts owner turn ends");
        var nextPlayer = WorldTurns.EndTurn(nextEnemy, nextEnemy.Revision, turns).State;
        Check(Unit(nextPlayer, caster.Id).RemainingMovement == 9, "next turn movement buff");
        var expired = WorldTurns.EndTurn(nextPlayer, nextPlayer.Revision, turns).State;
        Check(Unit(expired, caster.Id).Combatant.Statuses.Count == 0, "status expires");
        var plain = WorldTurns.EndTurn(expired, expired.Revision, turns).State;
        Check(Unit(plain, caster.Id).RemainingMovement == 6, "movement returns to base");

        var poison = Cast(world, "poison", enemy.Id).State;
        Check(Unit(poison, enemy.Id).Combatant.Statuses.Single().SourceOwner == caster.Owner, "status origin");
        var tick = WorldTurns.EndTurn(poison, poison.Revision, turns);
        Check(Unit(tick.State, enemy.Id).Combatant.Health == 96 && tick.Notices.Any(n => n.Kind == "battle.damage" && n.Subject == enemy.Id), "status damage and notification");
        Check(tick.State.ExtensionState[enemy.Id + "/status.example.poisoned/ticks"] == 1, "status Lua counter");
        var stacked = poison;
        for (int i = 0; i < 3; i++) stacked = Cast(Rearm(stacked), "poison", enemy.Id).State;
        Check(Unit(stacked, enemy.Id).Combatant.Statuses.Single().Stacks == 3, "stack cap");
        Check(Unit(bus.Dispatch(stacked, "turn.start", enemy.Id, new(stacked.RandomState)), enemy.Id).Combatant.Health == 88, "Lua stack context");
        var doomed = poison with { Units = poison.Units.Select(u => u.Id == enemy.Id ? u with { Combatant = u.Combatant with { Health = 1 } } : u).ToArray() };
        var poisonDeath = WorldTurns.EndTurn(doomed, doomed.Revision, turns);
        Check(poisonDeath.State.DestroyedScenarioUnits.Contains(enemy.Id) && poisonDeath.Notices.Any(n => n.Kind == "unit.destroyed"), "periodic unified death");
        var contaminated = world with { Units = world.Units.Select(u => u.Id == ally.Id ? u with { Combatant = u.Combatant with {
            Statuses = [new("example.poisoned", 2, SourceOwner: "loser")] } } : u).ToArray(),
            ExtensionState = new Dictionary<string, long> { [ally.Id + "/status.example.poisoned/ticks"] = 3 } };
        var clean = Cast(contaminated, "cleanse", ally.Id).State;
        Check(Unit(clean, ally.Id).Combatant.Statuses.Count == 0 && clean.ExtensionState.Count == 1, "cleanse removes state but retains cooldown");
        Check(Unit(Cast(Rearm(rally), "rally").State, caster.Id).Combatant.Statuses.Single().Stacks == 1, "refresh does not stack");

        var defender = Unit(saved, ally.Id).Combatant;
        var plainDamage = Ew4Combat.Calculate(enemy.Combatant, ally.Combatant, 1, new(17), new() { Extensions = bus });
        var protectedDamage = Ew4Combat.Calculate(enemy.Combatant, defender, 1, new(17), new() { Extensions = bus });
        Check(protectedDamage.DefenderDamage < plainDamage.DefenderDamage, "incoming status participates in real combat");
        var countersExpired = tick.State;
        for (int i = 0; i < 3; i++) countersExpired = bus.ExpireStatuses(countersExpired, enemy.Owner);
        Check(!countersExpired.ExtensionState.Keys.Any(k => k.StartsWith(enemy.Id + "/status.")), "expiry clears periodic counters");
        var poisonedOnlyEnemy = doomed with { Cells = doomed.Cells.Select(c => c with { Owner = "winner" }).ToArray(),
            Units = doomed.Units.Where(u => u.Owner == "winner" || u.Id == enemy.Id).ToArray() };
        var eliminated = WorldTurns.EndTurn(poisonedOnlyEnemy, poisonedOnlyEnemy.Revision, turns).State;
        Check(eliminated.Factions.Single(f => f.Id == enemy.Owner).Defeated && eliminated.ActiveFaction == caster.Owner, "lethal start status skips eliminated faction");
        var dialogueWorld = poisonedOnlyEnemy with { Events = [new() { Id = "poison.death", DialogueId = "last.dialogue",
            Rules = [new() { Trigger = TriggerKind.UnitDestroyed, UnitId = enemy.Id }] }] };
        var deathDialogue = WorldTurns.EndTurn(dialogueWorld, dialogueWorld.Revision, turns).State;
        Check(deathDialogue.PendingDialogue == "last.dialogue" && deathDialogue.Phase == TurnPhase.EndFactionEvents,
            "lethal status pauses eliminated faction at death dialogue");
        var acknowledged = WorldTurns.Resume(deathDialogue, deathDialogue.Revision, turns, "last.dialogue").State;
        Check(acknowledged.ActiveFaction == caster.Owner && acknowledged.PendingDialogue == null, "death dialogue resumes next surviving faction");
        var slowing = new ExtensionStatus { Id = "test.slow", Effects = [new() { Event = "movement.modify", Operation = "movement_add", Value = -4 }] };
        var slowSkill = new ExtensionSkill { Id = "test.slow_skill", Kind = "active", Target = "self", EndsAction = false,
            Effects = [new() { Event = "skill.activate", Operation = "apply_status", Status = slowing.Id, Duration = 2 }] };
        var slowBus = new RuleEventBus(bus.Skills.Values.Append(slowSkill).ToDictionary(s => s.Id), bus.Scripts,
            bus.Statuses.Values.Append(slowing).ToDictionary(s => s.Id));
        var fast = world with { Units = world.Units.Select(u => u.Id != caster.Id ? u : u with { RemainingMovement = 6,
            Combatant = u.Combatant with { SkillSlots = u.Combatant.SkillSlots.Append(new(slowSkill.Id, "slow")).ToArray() } }).ToArray() };
        var slowed = WorldSkillActions.Execute(fast, 0, caster.Id, "slow", null, slowBus, move).State;
        Check(Unit(slowed, caster.Id).RemainingMovement == 2 && slowBus.MovementBudget(Unit(slowed, caster.Id).Combatant) == 2, "negative movement modifier clips remaining movement");
        var commanderWorld = saved with { Commanders = [new() { Id = "test.general", Owner = caster.Owner }] };
        var commanderRules = new WorldCommanderRules { Extensions = bus, Definitions = new Dictionary<string, Commander> {
            ["test.general"] = new() { Id = "test.general", Movement = 2 } } };
        var assigned = WorldCommanders.Assign(commanderWorld, commanderWorld.Revision, caster.Id, "test.general", commanderRules).State;
        Check(Unit(assigned, caster.Id).Combatant.Statuses.Count == 1 && Unit(assigned, caster.Id).ExtensionMovementLimit == 11, "commander assignment preserves status and recomputes movement");
        var detached = WorldCommanders.Detach(assigned, assigned.Revision, caster.Id, commanderRules).State;
        Check(Unit(detached, caster.Id).Combatant.Statuses.Count == 1 && Unit(detached, caster.Id).ExtensionMovementLimit == 9, "commander removal preserves status");

        var errorRuntime = new ExtensionScriptRuntime(new Dictionary<string, string> { ["rewrite/scripts/error.lua"] =
            "function fail(ctx) if (ctx.state.hits or 0) > 0 then error('second target') end return {value=5,state={hits=1}} end" });
        var errorSkill = bus.Skills["example.blast"] with { Effects = [bus.Skills["example.blast"].Effects[0] with {
            Script = "rewrite/scripts/error.lua", Function = "fail" }] };
        var errorBus = new RuleEventBus(new Dictionary<string, ExtensionSkill> { [errorSkill.Id] = errorSkill }, errorRuntime);
        Reject(() => WorldSkillActions.Execute(world, 0, caster.Id, "unit.blast", null, errorBus, move, enemy.Cell), "Lua failure on second area target");

        var failing = bus.Skills["example.blast"] with { Effects = [bus.Skills["example.blast"].Effects[0],
            new() { Event = "skill.activate", Operation = "damage", Scope = "area", Radius = 1, Value = double.NaN }] };
        var failingBus = new RuleEventBus(bus.Skills.Values.Where(s => s.Id != failing.Id).Append(failing).ToDictionary(s => s.Id), bus.Scripts, bus.Statuses);
        Reject(() => WorldSkillActions.Execute(world, 0, caster.Id, "unit.blast", null, failingBus, move, enemy.Cell), "partial area failure");
        Check(initialBytes.SequenceEqual(WorldSave.Encode(world)), "actions preserve original world and RNG");
        var previousManifest = files["rewrite/extension.json"];
        files["rewrite/extension.json"] = Encoding.UTF8.GetBytes("{\"schema\":1,\"apiVersion\":1,\"skills\":[\"rewrite/definitions/skills.json\"]}");
        Reject(() => ExtensionLoader.Load(Read), "API 2 fields in API 1"); files["rewrite/extension.json"] = previousManifest;
        void InvalidEffect(ExtensionEffect effect, string label)
        {
            var previous = files["rewrite/definitions/skills.json"];
            files["rewrite/definitions/skills.json"] = JsonSerializer.SerializeToUtf8Bytes(new[] { new ExtensionSkill {
                Id = "test.invalid", Kind = "active", Target = "cell", Effects = [effect] } }, ExtensionLoader.Json);
            Reject(() => ExtensionLoader.Load(Read), label); files["rewrite/definitions/skills.json"] = previous;
        }
        InvalidEffect(new() { Event = "skill.activate", Operation = "apply_status", Status = "unknown.status", Duration = 2 }, "unknown status");
        InvalidEffect(new() { Event = "skill.activate", Operation = "damage", Scope = "area", Radius = 33 }, "oversized area");
        InvalidEffect(new() { Event = "skill.activate", Operation = "money", Scope = "area", Radius = 1 }, "area resource multiplication");
        InvalidEffect(new() { Event = "skill.activate", Operation = "apply_status", Status = "example.vigor" }, "zero duration");
        Reject(() => new RuleEventBus(bus.Skills, bus.Scripts, bus.Statuses).RegisterUnitTemplates([]), "missing summon profile");
        byte[] FullRead(string path) => files.TryGetValue(path, out var bytes) ? bytes : File.ReadAllBytes(path.StartsWith("data/")
            ? Path.Combine(root, "rewrite/data/legacy", path[5..]) : Path.Combine(root, "rewrite/data", path));
        var content = new ClientContent(FullRead, ["conquest1.btl"]);
        Check(content.Start("conquest1", "fra").Rules.Movement.Extensions!.Skills.Count == 6
            && content.Extensions.Bus.UnitTemplates.Count > 0, "real scenario API 2 and templates");
        var report = Path.Combine(root, "rewrite/reports/common-skills-20261021"); Directory.CreateDirectory(report);
        File.WriteAllText(Path.Combine(report, "core-results.json"), JsonSerializer.Serialize(new { checks, errors = 0 }));
        return checks;
    }
}
