using System.Text.Json;
using Ew4.Core;
using Ew4.Legacy;
using Ew4.Persistence;

namespace Ew4.Client;

public sealed record ClientCommand(string Op = "", long Revision = -1, string? Scenario = null, string? Country = null,
    string? Unit = null, string? Target = null, int Cell = -1, string? Offer = null, string? Commander = null, string? Item = null, JsonElement? Tutorial = null, int Group = 0, int Slot = 0, int Mode = 0);
public sealed record ClientSlot(string Scenario, string? Country, byte[] Session, MovementHistoryState? MovementHistory = null, JsonElement? Tutorial = null,
    int Round = 0, DateTimeOffset? SavedAt = null);

// Both hosts serialize commands here, so rendering cannot bypass turn ownership or stale-state checks.
public sealed partial class ClientGame(ClientContent content, string saveDirectory)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly object gate = new();
    private LegacyScenarioStart? game;
    private string? country;
    private JsonElement? tutorialCheckpoint;
    private readonly MovementHistory movementHistory = new();
    private string Slot => Path.Combine(saveDirectory, "session.json");
    public string Dispatch(string path, string body)
    {
        lock (gate)
        {
            try
            {
                object result = path switch
                {
                    "/api/catalog" => new { scenarios = content.Scenarios, chapters = content.Chapters, presentations = content.Presentations,
                        unlockedCampaigns = Account().UnlockedCampaigns, hasSave = File.Exists(Slot), extensionLayouts = content.Extensions.Layouts },
                    "/api/countries" => Countries(Command(body)),
                    "/api/headquarters" => Headquarters(),
                    "/api/commander" => CommanderAction(Command(body)),
                    "/api/commerce" => Commerce(Command(body)),
                    "/api/exchange" => Exchange(Command(body)),
                    "/api/start" => Start(Command(body)),
                    "/api/action" => Action(Command(body)),
                    "/api/save" => Save(JsonSerializer.Deserialize<ClientCommand>(body, Json) ?? new()),
                    "/api/saves" => Saves(Command(body)),
                    "/api/load" => Load(JsonSerializer.Deserialize<ClientCommand>(body, Json) ?? new()),
                    _ => throw new RuleViolation("client.route", "Unknown command")
                };
                return JsonSerializer.Serialize(new { ok = true, data = result }, Json);
            }
            catch (Exception error) when (error is RuleViolation or IOException or JsonException or InvalidOperationException or ArgumentException or KeyNotFoundException)
            {
                return JsonSerializer.Serialize(new { ok = false, code = error is RuleViolation rule ? rule.Code : "client.failure", message = error.Message }, Json);
            }
        }
    }
    private static ClientCommand Command(string body) => JsonSerializer.Deserialize<ClientCommand>(body, Json)
        ?? throw new RuleViolation("client.command", "Missing command payload");
    private object Countries(ClientCommand command) => content.Battle(command.Scenario!).Factions
        .Where(f => f.Camp != 4).Select(f => new { id = f.CountryId, name = content.Label(f.CountryId), camp = f.Camp }).ToArray();
    private object Start(ClientCommand command)
    {
        if (!CampaignProgress.Accessible(content, Account(), command.Scenario!))
            throw new RuleViolation("campaign.locked", "请先完成对应黄圈目标并赢得战役，解锁此隐藏关卡");
        var next = content.Start(command.Scenario!, command.Country, Account());
        game = next; country = command.Country;
        tutorialCheckpoint = null;
        movementHistory.Reset(next.Session.World);
        AutoSave();
        return View();
    }
    private object Load(ClientCommand command)
    {
        var slot = ReadSlot(SavePath(command.Slot, command.Mode, true));
        var start = content.Start(slot.Scenario, slot.Country);
        var state = SessionSave.Decode(slot.Session, new(start.Session.ContentRevision, start.Session.World.ContentRevision, start.Session.Account.ContentRevision));
        if (start.Session.World.Jurisdictions != null)
        {
            if (state.World.Jurisdictions == null)
                state = state with { World = state.World with { Jurisdictions = start.Session.World.Jurisdictions.Copy() with { CenterCaptureVersion = 0 } } };
            state = state with { World = JurisdictionClaims.UpgradeCapturedDistricts(state.World, start.Session.World) };
        }
        var profile = Account();
        var unlocks = profile.UnlockedCampaigns.Union(state.Account.UnlockedCampaigns, StringComparer.Ordinal).ToArray();
        if (profile.Revision > state.Account.Revision) state = state with { Account = profile };
        if (!state.Account.UnlockedCampaigns.SequenceEqual(unlocks))
            state = state with { Account = state.Account with { Revision = checked(Math.Max(profile.Revision, state.Account.Revision) + 1), UnlockedCampaigns = unlocks } };
        state = state with { Account = CampaignProgress.Settle(content, state) };
        state.Validate();
        StoreAccount(state.Account);
        game = start with { Session = state }; country = slot.Country;
        tutorialCheckpoint = slot.Tutorial;
        movementHistory.Load(slot.MovementHistory, state.World);
        return View();
    }
    private object Save(ClientCommand command)
    {
        var current = Require();
        string path = SavePath(command.Slot, command.Mode);
        if(command.Mode != 0 && content.Scenarios.Single(s => s.Id == current.Session.World.ScenarioId).Mode != command.Mode)
            throw new RuleViolation("save.mode", "Save mode does not match the current game");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        tutorialCheckpoint = command.Tutorial;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new ClientSlot(current.Session.World.ScenarioId, country, SessionSave.Encode(current.Session), movementHistory.Save(), tutorialCheckpoint,
            current.Session.World.Round, DateTimeOffset.Now), Json);
        string temporary = path + ".tmp";
        using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None)) { stream.Write(bytes); stream.Flush(true); }
        if (File.Exists(path)) File.Replace(temporary, path, path + ".bak"); else File.Move(temporary, path);
        return new { saved = true };
    }
    private LegacyScenarioStart Require() => game ?? throw new RuleViolation("client.no_session", "No game is open");
    private object Action(ClientCommand command)
    {
        var current = Require(); var session = current.Session; var world = session.World; var rules = current.Rules;
        if (command.Op == "state") return View();
        if (command.Revision != session.Revision) throw new RuleViolation("client.stale", "State changed; refresh the command");
        var active = world.Factions.Single(f => f.Id == world.ActiveFaction);
        if (command.Op == "select") return Selection(command.Cell);
        if (command.Op == "dialogue")
        {
            if (world.PendingDialogue == null) throw new RuleViolation("client.dialogue", "No dialogue is waiting");
            WorldChange change = world.Phase == TurnPhase.Active
                ? new(WorldEvents.Acknowledge(world, world.PendingDialogue) with { Revision = checked(world.Revision + 1) }, Array.Empty<WorldNotice>())
                : WorldTurns.Resume(world, world.Revision, rules.Turns, world.PendingDialogue);
            Commit(change); return View();
        }
        if (command.Op == "ai")
        {
            if (active.Human || world.PendingDialogue != null) throw new RuleViolation("client.ai_turn", "AI does not own the active turn");
            var production = rules.Production[active.Id];
            var plan = WorldAi.PlanStep(world, rules.Ai, production, rules.Turns);
            var combat = AiCombat(world, plan);
            Commit(WorldAi.ExecuteStep(world, plan, rules.Ai, production, rules.Turns, rules.Battle)); return View(world, combat: combat);
        }
        if (active.Id != session.PlayerFaction || !active.Human) throw new RuleViolation("client.player_turn", "Player does not own the active turn");
        if (command.Op == "undo")
        {
            var undo = movementHistory.Undo(world, command.Unit);
            string unit = undo.Change.Notices[0].Subject;
            Commit(undo.Change);
            return View(null, new[] { new { unit, path = undo.Path } });
        }
        if (command.Op == "deploy")
        {
            var change = RosterDeployment.Deploy(session, session.Revision, command.Unit!, command.Commander!, rules.Roster);
            game = current with { Session = change.State, Notices = change.Notices };
            StoreAccount(change.State.Account);
            return View();
        }
        if (command.Op == "hire")
        {
            var hired = SessionActions.DeployTavern(session, session.Revision, command.Unit!, command.Offer!, rules.Tavern);
            game = current with { Session = hired.State, Notices = hired.Notices };
            StoreAccount(hired.State.Account);
            AutoSave();
            return View();
        }
        var preview = command.Op == "attack" ? WorldBattle.Preview(world, world.Revision, command.Unit!, command.Target!, rules.Battle) : null;
        WorldChange action = command.Op switch
        {
            "skill" => WorldSkillActions.Execute(world, world.Revision, command.Unit!, command.Offer!, command.Target, content.Extensions.Bus, rules.Movement),
            "move" => WorldActions.Move(world, world.Revision, command.Unit!, command.Cell, rules.Movement),
            "disband" => WorldDisband.Execute(world, world.Revision, command.Unit!, DisbandConfiguration()),
            "attack" => WorldBattle.Attack(world, world.Revision, command.Unit!, command.Target!, rules.Battle),
            "end" => WorldTurns.EndTurn(world, world.Revision, rules.Turns),
            "train" => WorldImprovements.Train(world, world.Revision, command.Unit!, rules.Training),
            "upgrade" => WorldImprovements.UpgradeBuilding(world, world.Revision, command.Cell, rules.Upgrades),
            "transport" => WorldSupportActions.BuyTransport(world, world.Revision, command.Unit!, rules.Transport),
            "installation" => WorldSupportActions.BuildInstallation(world, world.Revision, command.Unit!, rules.Installations[command.Offer!]),
            "recruit" => WorldActions.Recruit(world, world.Revision, command.Cell, "client.unit." + world.Revision, rules.Recruitment[active.Id][command.Offer!]),
            _ => throw new RuleViolation("client.command", "Unknown action")
        };
        int[]? movedPath = command.Op == "move" ? WorldActions.Reachable(world, command.Unit!, rules.Movement)[command.Cell].Path.ToArray() : null;
        Commit(action);
        if (movedPath != null) movementHistory.Record(world, action.State, command.Unit!, movedPath);
        if (command.Op == "attack") movementHistory.Attack(command.Unit!);
        if (command.Op == "disband") { movementHistory.Forget(command.Unit!); AutoSave(); }
        return View(world, combat: preview == null ? null : new { attacker = preview.Attacker, defender = preview.Defender, counterattack = preview.Main.Counterattack,
            damage = preview.Main.DefenderDamage, counterDamage = preview.Main.AttackerDamage,
            critical = preview.Main.OffensiveProc && !preview.Main.DefensiveProc, counterCritical = false,
            splashTarget = preview.SplashTarget, splashDamage = preview.SplashDamage });
    }
    private void Commit(WorldChange change)
    {
        var current = Require();
        if (content.Extensions.Enabled)
        {
            var world = change.State;
            foreach (string id in world.Units.Where(u => !current.Session.World.Units.Any(old => old.Id == u.Id)).Select(u => u.Id).ToArray())
                world = content.Extensions.Bus.Dispatch(world, "unit.recruited", id, new Ew4Random(world.RandomState));
            if (current.Rules.Turns.Victory.Custom != null && world.Outcome == BattleOutcome.Continue)
            {
                var outcome = WorldVictory.Evaluate(world, current.Rules.Turns.PlayerCamp!, current.Rules.Turns.Victory, current.Rules.Turns.NeutralCamps);
                if (outcome != BattleOutcome.Continue) world = world with { Outcome = outcome, Phase = TurnPhase.Finished };
            }
            change = change with { State = world };
        }
        var session = SessionActions.CommitWorld(current.Session, current.Session.Revision, change);
        var settled = session.State with { Account = CampaignProgress.Settle(content, session.State) };
        game = current with { Session = settled, Notices = change.Notices };
        StoreAccount(settled.Account);
        movementHistory.Observe(change.State);
        if(change.State.Round != current.Session.World.Round || change.State.ActiveFaction != current.Session.World.ActiveFaction || change.State.Outcome != current.Session.World.Outcome)
            AutoSave();
    }
    private object Selection(int cell)
    {
        var current = Require(); var world = current.Session.World;
        if (cell < 0 || cell >= world.Cells.Count) throw new RuleViolation("map.cell", "Cell outside map");
        bool ready = world.Phase == TurnPhase.Active && world.PendingDialogue == null && world.ActiveFaction == current.Session.PlayerFaction;
        var unit = world.Units.FirstOrDefault(u => u.Cell == cell);
        var offers = ready ? current.Rules.Recruitment[world.ActiveFaction].Values.Where(o => WorldActions.CanRecruit(world, cell, o))
            .Select(o => new { o.Id, profile = o.Template?.Profile.Id, o.Cost }).ToArray() : [];
        var occupied = world.Units.Select(u => u.Cell).ToHashSet();
        var range = ready && unit?.Owner == world.ActiveFaction
            ? WorldActions.Reachable(world, unit.Id, current.Rules.Movement).Keys.Where(c => !occupied.Contains(c)).ToArray() : [];
        var targets = ready && unit?.Owner == world.ActiveFaction ? AttackTargets(world, unit).Select(u => u.Cell).ToArray() : [];
        var disbandRules = DisbandConfiguration();
        var refund = ready && unit != null && WorldDisband.CanDisband(world, unit.Id, disbandRules)
            ? WorldDisband.Refund(unit, disbandRules.RecruitmentCosts[unit.Combatant.Profile.Id]) : null;
        return new { cell, range, targets, offers, disband = refund,
            skills = ready && unit?.Owner == world.ActiveFaction ? ActiveSkills(world, unit) : [],
            tavern = ready && world.Cells[cell].Owner == world.ActiveFaction && Service(cell) == "bar",
            shop = ready && world.Cells[cell].Owner == world.ActiveFaction && Service(cell) == "shop",
            market = ready && world.Cells[cell].Owner == world.ActiveFaction && Service(cell) == "trade",
            upgrade = ready && WorldImprovements.CanUpgradeBuilding(world, cell, current.Rules.Upgrades),
            deploy = ready && unit?.Owner == world.ActiveFaction && unit.CommanderId == null && world.Cells[cell].Owner == world.ActiveFaction,
            unit = unit?.Id, installations = ready && unit?.Owner == world.ActiveFaction ? current.Rules.Installations
                .Where(p => WorldSupportActions.CanBuildInstallation(world, unit.Id, p.Value)).Select(p => p.Key).ToArray() : [],
            undo = ready && unit != null && movementHistory.Available(world, unit.Id) != null,
            transport = ready && unit?.Owner == world.ActiveFaction && WorldSupportActions.CanBuyTransport(world, unit.Id, current.Rules.Transport) };
    }
    private static IEnumerable<UnitState> AttackTargets(WorldState world, UnitState unit)
    {
        var map = new HexMap(world.Width, world.Height);
        var camps = world.Factions.ToDictionary(f => f.Id, f => f.Camp);
        return world.Units.Where(target => camps[target.Owner] != camps[unit.Owner]
            && AttackLegality.Check(true, true, unit.Combatant.ConstructionTurns, unit.Acted, unit.Morale.Effective,
                map.Distance(unit.Cell, target.Cell), unit.Combatant.Profile.MinimumRange, unit.Combatant.Profile.MaximumRange) == AttackBlock.None);
    }
    private object View(WorldState? previous = null, object? movementOverride = null, object? combat = null)
    {
        var current = Require(); var session = current.Session; var world = session.World;
        var battle = content.Battle(world.ScenarioId);
        var factionColors = battle.Factions.Select((source, index) => (source, index))
            .ToDictionary(p => LegacyScenarioInitialization.FactionId(world.ScenarioId, p.index, p.source), p => p.source.Color);
        var factionSources = battle.Factions.Select((source, index) => (source, index))
            .ToDictionary(p => LegacyScenarioInitialization.FactionId(world.ScenarioId, p.index, p.source), p => p.source);
        var sites = battle.EffectiveSites.ToDictionary(s => (int)s.Cell);
        var originalUnits = battle.Armies.Select((army, index) => (army, id: world.ScenarioId + ".unit." + index))
            .ToDictionary(p => p.id, p => p.army);
        var movements = new List<object>();
        if (previous != null)
        {
            var before = previous.Units.ToDictionary(u => u.Id);
            foreach (var unit in world.Units)
                if (before.TryGetValue(unit.Id, out var old) && old.Cell != unit.Cell
                    && WorldActions.Reachable(previous, old.Id, current.Rules.Movement).TryGetValue(unit.Cell, out var reachable))
                    movements.Add(new { unit = unit.Id, path = reachable.Path });
        }
        string? BuildingImage(int index, CellState cell)
        {
            if (cell.BuildingKind == null) return null;
            if (cell.BuildingKind == "city" && sites.TryGetValue(index, out var site)
                && content.Catalogs.Buildings.Capitals.TryGetValue(site.State8, out var capital)) return capital.Image;
            var definition = content.Catalogs.Buildings.Definitions.Values.SingleOrDefault(d => d.Kind == cell.BuildingKind);
            return definition?.Levels.ElementAtOrDefault(Math.Max(0, cell.BuildingLevel - 1))?.Image;
        }
        object TileInfo(int index, CellState cell)
        {
            var definition = content.Catalogs.Buildings.Definitions.Values.SingleOrDefault(d => d.Kind == cell.BuildingKind);
            var level = definition?.Levels.ElementAtOrDefault(Math.Max(0, cell.BuildingLevel - 1));
            var income = cell.IncomeOverride ?? level?.Turn.Income ?? new Resources(0, 0, 0);
            if (cell.IncomeOverride == null && current.Rules.Turns.SiteIncomeOverrides.TryGetValue(index, out var specific)
                && specific.Kind == cell.BuildingKind)
                income = new(specific.Money ?? income.Money, specific.Industry ?? income.Industry, specific.Food ?? income.Food);
            content.Catalogs.Combat.Context.TerrainDefense.TryGetValue(cell.DefenseTerrain, out var penalties);
            return new { income, defense = level?.DefensePercent ?? 0, penalties };
        }
        bool CanAttack(UnitState unit) => unit.Owner == world.ActiveFaction && world.Phase == TurnPhase.Active
            && world.PendingDialogue == null && AttackTargets(world, unit).Any();
        object? DialoguePresentation()
        {
            if (world.PendingDialogue == null || !current.Rules.Dialogues.TryGetValue(world.PendingDialogue, out var entry)) return null;
            string? portrait = content.Catalogs.Runtime.Commanders.TryGetValue(entry.Commander, out var speaker)
                ? speaker.Definition.DisplayName : null;
            return new { id = entry.Id, text = entry.Text, left = entry.Left, portrait = portrait == null ? null : portrait + ".png",
                name = portrait == null ? "" : content.Label(portrait) };
        }
        return new
        {
            session.Revision, scenario = world.ScenarioId, map = content.MapNames[battle.MapId], world.Width, world.Height,
            tutorialCheckpoint,
            cameraBounds = new { left = battle.Left * 64 - 52, top = (battle.Top - 1) * 54 - 20,
                width = battle.Width * 64 + 72, height = (battle.Height - 1) * 54 + 112 },
            movements = movementOverride ?? movements,
            combat,
            recruited = previous == null ? [] : world.Units.Where(u => !previous.Units.Any(old => old.Id == u.Id)).Select(u => u.Id).ToArray(),
            undoUnit = movementHistory.Available(world)?.Unit,
            world.Round, phase = world.Phase.ToString(), outcome = world.Outcome.ToString(), world.ActiveFaction, session.PlayerFaction,
            dialogue = world.PendingDialogue == null ? null : current.Rules.Dialogues.TryGetValue(world.PendingDialogue, out var dialogue) ? dialogue.Text : world.PendingDialogue,
            dialoguePresentation = DialoguePresentation(),
            medals = session.Account.Medals,
            factions = world.Factions.Select(f => new { f.Id, f.Name, f.Camp, f.Human, f.Defeated, f.Resources,
                neutral = WorldDiplomacy.NeutralTo(world, f.Id, session.PlayerFaction, current.Rules.Turns.NeutralCamps),
                country = factionSources[f.Id].CountryId, flag = factionSources[f.Id].Variant,
                color = factionColors[f.Id] }),
            cells = world.Cells.Select((c, i) => new { c.Owner, c.Name, c.BuildingKind, c.BuildingLevel, c.Terrain.Active, c.Terrain.Sea, c.Objective, c.InstallationKind, c.Environment,
                objectiveMarker = c.Objective ? 1 : sites.TryGetValue(i, out var markedSite) && markedSite.Marker == 2 ? 2 : 0,
                jurisdictionCenter = world.Jurisdictions?.CellCenters[i] is int center && center >= 0 ? center : (int?)null,
                tileInfo = TileInfo(i, c),
                facility = sites.TryGetValue(i, out var service) ? service.State6 switch { 1 => "trade", 2 => "shop", 3 => "bar", _ => (string?)null } : null,
                buildingImage = BuildingImage(i, c), buildingStyle = sites.TryGetValue(i, out var site) ? site.State7 : 1 }),
            units = world.Units.Select(u => new { u.Id, u.Owner, u.Cell, profile = u.Combatant.Profile.Id, category = u.Combatant.Profile.Category,
                skills = SkillViews(u.Combatant), traits = u.Combatant.Profile.Traits.Where(content.Extensions.Bus.Skills.ContainsKey)
                    .Select(id => SkillView(new SkillSlot(id, "trait." + id))).ToArray(),
                health = u.Combatant.Health, maxHealth = u.Combatant.Profile.MaxHealth, u.RemainingMovement, u.Acted, u.CommanderId, u.Transport, u.Burning, u.Objective,
                objectiveMarker = u.Objective ? 1 : originalUnits.TryGetValue(u.Id, out var markedUnit) && markedUnit.Objective == 2 ? 2 : 0,
                canMove = u.Combatant.Profile.Category != "fort" && u.RemainingMovement > 0 && u.Morale.Effective != -3 && u.Combatant.ConstructionTurns == 0,
                canAttack = CanAttack(u),
                veterancy = u.Veterancy,
                facing = originalUnits.TryGetValue(u.Id, out var source) ? source.Facing : 1,
                commanderImage = session.Account.Roster.FirstOrDefault(c => c.Id == u.CommanderId) is OwnedCommander owned
                    ? content.Catalogs.Accounts.Rules().Definitions[owned.DefinitionId].DisplayName
                    : u.CommanderId == u.Id + ".commander" && originalUnits.TryGetValue(u.Id, out var commanded)
                    && content.Catalogs.Runtime.Commanders.TryGetValue(commanded.Commander, out var commander) ? commander.Definition.DisplayName : null }),
            notices = current.Notices
        };
    }
}
