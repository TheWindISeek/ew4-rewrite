// PlaySession — 单局游玩聚合根（由 PlayCoordinator 懒创建；HTTP 入口见 PlayRouteRegistry）。
//
// 阅读顺序（按职责）：
//   1. 开局/离局/存读档 — Countries, Start, Load, Save, Leave, Saves*
//   2. 局内命令 — Action（移动/攻击/技能/AI/回合等）
//   3. 状态机 — ApplyEngine*, Commit*, InstallSession, SettleBattle
//   4. 前端状态 — View, Selection, UnitInfo, BattleResultView, ScenarioPopupView
//   5. 总部账号 — Account*, Headquarters, CommanderAction（与当前局弱耦合）
//   6. 地图服务 — Commerce, Exchange, Consumables, TavernView, TradingRules
//   7. Route* — PlayRouteRegistry 调用的薄包装（解析 JSON → 上面对应方法）
//

using Ew4.Platform.Ai;
using Ew4.Platform.Content;
using Ew4.Platform.Content.CatalogLoad;
using Ew4.Platform.Content.StaticData;
using Ew4.Platform.Engine;
using Ew4.Platform.Persistence.Serialization;
using Ew4.Platform.Persistence;
using Ew4.Platform.Play.Match;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;

namespace Ew4.Service.Play;

/// <summary>客户端动作报价：资源花费与当前势力是否负担得起。</summary>
public sealed record ClientActionQuote(Resources Cost, bool Affordable);

/// <summary>技能/状态在 Play API 中的展示 DTO。</summary>
public sealed record PlaySkillView(string Id, string InstanceId, int Level, int Order, float? X, float? Y,
    string Name, string Description, string? Icon, string Kind, string Target, int Cooldown, Resources Cost);

public sealed partial class PlaySession(PlayContent content, string saveDirectory, IEngine engine, SlotSaveEnvelope.Binding? slotEnvelope = null)
{
    public static readonly JsonSerializerOptions Json = PlayCommandJson.Options;
    private StaticDataScenarioStart? game;
    private string? country;
    private JsonElement? tutorialCheckpoint;
    /// <summary>单局阻塞队列（对白/剧情选择）；Engine 状态不再持有。</summary>
    private readonly MatchFlow flow = new();
    /// <summary>单槽撤销：只回到最近一次移动之前。</summary>
    private readonly UndoSlot undo = new();
    private string? PendingFlow => flow.Head?.Id;
    /// <summary>AI 私有随机状态；不属于战场状态，随单局存档保存。</summary>
    private ulong aiRandom = NewAiSeed();
    private static ulong NewAiSeed() => (ulong)Random.Shared.NextInt64(1, long.MaxValue);
    /// <summary>当前 mod 存档目录下的自动存档路径（槽位 0 / session.json）。</summary>
    private string Slot => SaveDirectoryLayout.AutoSavePath(saveDirectory);

    /// <summary>开局前：列出关卡可选势力（国旗、阵营、颜色）。</summary>
    private object Countries(PlayCommand command) => content.Battle(command.Scenario!).Factions
        .Where(f => f.Camp != 4).Select(f =>
        {
            var country = content.Country(f.CountryId);
            var resolved = FactionAppearanceResolve.Resolve(
                null,
                country?.Flag,
                country?.FlagImage,
                country?.Color,
                f.Variant,
                f.Color,
                content.Countries.Canonical(f.CountryId));
            return new { id = content.Countries.Canonical(f.CountryId), name = content.CountryName(f.CountryId), camp = f.Camp,
                flag = resolved.Flag, flagImage = resolved.FlagImage, color = resolved.Color };
        }).ToArray();
    /// <summary>关闭当前局，清空内存中的 game、移动历史与教程检查点。</summary>
    private object Leave(PlayCommand command)
    {
        if (game != null && command.Revision != game.Session.Revision)
            throw new RuleViolation("client.stale", "State changed; refresh the command");
        EndBattleProfile();
        game = null;
        country = null;
        tutorialCheckpoint = null;
        undo.Clear();
        flow.Clear();
        return new { left = true };
    }
    /// <summary>新开一局：校验战役解锁与本地多人，初始化 session、写账号并自动存档。各阶段见 PlaySession.Steps.cs，Plan 里逐个作为 Step 运行。</summary>
    private object Start(PlayCommand command)
    {
        ValidateLocalSelection(command);
        EndBattleProfile();
        CheckUnlocked(command);
        InstallStarted(command, OpenStart(command));
        AutoSave();
        return View();
    }

    /// <summary>从槽位读档：解码 session、必要时迁移装备数值、合并账号解锁与辖区。各阶段见 PlaySession.Steps.cs。</summary>
    private object Load(PlayCommand command)
    {
        EndBattleProfile();
        PlaySlot slot = ReadSlot(SavePath(command.Slot, command.Mode, true));
        InstallLoaded(slot, DecodeSlot(slot));
        AutoSave();
        return View();
    }

    /// <summary>玩家手动存档：写槽位，并提交持久账户（明确的用户操作）。</summary>
    private object Save(PlayCommand command)
    {
        var result = SaveSlot(command);
        CommitProfile();
        return result;
    }
    /// <summary>将当前局序列化写入指定槽位（含移动历史与教程检查点）；自动存档只走这里，不提交账户。</summary>
    private object SaveSlot(PlayCommand command)
    {
        var current = Require();
        string path = SavePath(command.Slot, command.Mode);
        if(command.Mode != 0 && content.Scenarios.Single(s => s.Id == current.Session.World.ScenarioId).Mode != command.Mode)
            throw new RuleViolation("save.mode", "Save mode does not match the current game");
        tutorialCheckpoint = command.Tutorial;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new PlaySlot(current.Session.World.ScenarioId, country, SessionSave.Encode(current.Session), flow.Pending, tutorialCheckpoint,
            current.Session.World.Round, DateTimeOffset.Now, current.Session.World.LocalMultiplayer?.HumanFactions, aiRandom), Json);
        PlaySlotPersistence.WriteInnerJson(path, bytes, slotEnvelope);
        return new { saved = true };
    }
    /// <summary>断言当前有进行中的局，否则抛出 client.no_session。</summary>
    private StaticDataScenarioStart Require() => game ?? throw new RuleViolation("client.no_session", "No game is open");
    /// <summary>局内主命令分发：state/AI/选格/移动/攻击/技能/部署/事件/回合结束等。</summary>
    private object Action(PlayCommand command)
    {
        var current = Require(); var session = current.Session; var world = session.World; var rules = current.Rules;
        if (command.Op == "state") return View();
        if (command.Revision != session.Revision) throw new RuleViolation("client.stale", "State changed; refresh the command");
        if (command.Op == "ai_batch") return AiBatch();
        var active = world.Factions.Single(f => f.Id == world.ActiveFaction);
        if (command.Op == "select") return Selection(command.Cell);
        if (command.Op == "event_choice")
        {
            flow.RequirePopup();
            ApplyEngine(new EventChoiceCommand(session.Revision, command.Offer!, command.Target!, flow.Waiting), replaceFlow: true);
            AutoSave();
            return View();
        }
        if (command.Op == "dialogue")
        {
            flow.RequireDialogue();
            ApplyEngine(new DialogueAckCommand(session.Revision, flow.Waiting), replaceFlow: true);
            return View();
        }
        if (command.Op == "ai")
        {
            if (active.Human || flow.Blocked) throw new RuleViolation("client.ai_turn", "AI does not own the active turn");
            return View(world, combat: AiStep());
        }
        if (active.Id != session.PlayerFaction || !active.Human) throw new RuleViolation("client.player_turn", "Player does not own the active turn");
        if (flow.Blocked) throw new RuleViolation("action.dialogue_pending", "A dialogue is waiting");
        if (command.Op == "use_item")
        {
            CommitSession(ConsumableItems.Use(session, session.Revision, command.Unit!, command.Item!,
                content.Catalogs.Items.Rules(), rules.Training));
            AutoSave();
            return View();
        }
        if (command.Op == "undo")
        {
            var back = undo.Take(session, command.Unit);
            back.Restored.Validate();
            InstallSession(current, back.Restored, Array.Empty<WorldNotice>());
            return View(null, new[] { new { unit = back.Unit, path = back.Path, instant = true } });
        }
        if (command.Op == "deploy")
        {
            CommitSession(RosterDeployment.Deploy(session, session.Revision, command.Unit!, command.Commander!, rules.Roster));
            AutoSave();
            return View();
        }
        if (command.Op == "hire")
        {
            CommitSession(SessionAccountActions.DeployTavern(session, session.Revision, command.Unit!, command.Offer!, rules.Tavern));
            AutoSave();
            return View();
        }

        long revision = session.Revision;
        switch (command.Op)
        {
            case "skill":
                throw new RuleViolation("skill.unavailable", "扩展主动技能已移除");
            case "move":
                var worldBeforeMove = world;
                var moved = ApplyEngine(new MoveCommand(revision, command.Unit!, command.Cell));
                object? playerMovements = null;
                if (moved.Events.OfType<UnitMovedEvent>().FirstOrDefault() is { } movement)
                {
                    playerMovements = new[] { new { unit = movement.UnitId, path = movement.Path } };
                    if (!flow.Blocked && moved.Session.World.TriggeredScenarioEvents.Count == world.TriggeredScenarioEvents.Count)
                    {
                        undo.Record(session, moved.Session, movement.UnitId, movement.Path);
                    }
                }

                return View(worldBeforeMove, playerMovements);
            case "disband":
                ApplyEngine(new DisbandCommand(revision, command.Unit!));
                AutoSave();
                return View();
            case "attack":
                var preview = ApplyEngine(new AttackCommand(revision, command.Unit!, command.Target!))
                    .Events.OfType<AttackResolvedEvent>().FirstOrDefault()?.Preview;
                return View(world, combat: preview == null ? null : new
                {
                    attacker = preview.Attacker,
                    defender = preview.Defender,
                    counterattack = preview.Main.Counterattack,
                    damage = preview.Main.DefenderDamage,
                    counterDamage = preview.Main.AttackerDamage,
                    critical = preview.Main.OffensiveProc && !preview.Main.DefensiveProc,
                    counterCritical = false,
                    splashTarget = preview.SplashTarget,
                    splashDamage = preview.SplashDamage
                });
            case "end":
                ApplyEngine(new EndTurnCommand(revision));
                return View();
            case "train":
                ApplyEngine(new TrainCommand(revision, command.Unit!));
                return View();
            case "upgrade":
                ApplyEngine(new UpgradeBuildingCommand(revision, command.Cell));
                return View();
            case "transport":
                ApplyEngine(new BuyTransportCommand(revision, command.Unit!));
                return View();
            case "installation":
                ApplyEngine(new BuildInstallationCommand(revision, command.Unit!, command.Offer!));
                return View();
            case "recruit":
                ApplyEngine(new RecruitCommand(revision, command.Cell, command.Offer!, "client.unit." + world.Revision));
                return View();
            default:
                throw new RuleViolation("client.command", "Unknown action");
        }
    }

    /// <summary>提交前用扩展总线与回合规则 enrichment 世界变更。</summary>
    private WorldChange PrepareChange(WorldChange change)
    {
        var current = Require();
        return WorldCommitPipeline.Enrich(change, new WorldCommitContext(
            current.Session.World,
            current.Rules.Turns));
    }
    /// <summary>将 WorldChange 提交进 GameSession 并刷新聚合根。</summary>
    private void Commit(WorldChange change)
    {
        var current = Require();
        change = PrepareChange(change);
        var session = SessionActions.CommitWorld(current.Session, current.Session.Revision, change);
        InstallSession(current, session.State, change.Notices);
    }
    /// <summary>提交完整 SessionChange（含 SyncPlayer 与世界管线）。</summary>
    private void CommitSession(SessionChange change)
    {
        var current = Require();
        var world = PrepareChange(new(change.State.World, change.Notices));
        var next = SessionActions.SyncPlayer(change.State with { World = world.State });
        next.Validate();
        InstallSession(current, next, world.Notices);
    }
    /// <summary>取走世界里残留的阻塞项（旧存档、开局）并登记到队列；Engine 的结果本身已不含它们。</summary>
    private GameSession Adopt(GameSession session)
    {
        var (world, requests) = FlowExtraction.Take(session.World);
        flow.Raise(requests);
        return requests.Count == 0 ? session : session with { World = world };
    }
    /// <summary>安装新 session：战斗结算、写账号，必要时自动存档。</summary>
    private void InstallSession(StaticDataScenarioStart current, GameSession next, IReadOnlyList<WorldNotice> notices, bool flowChanged = false)
    {
        next = Adopt(next);
        var settled = SettleBattle(next);
        SetAccount(settled.Account);
        if (settled.BattleResult != null) CommitProfile(); // 战斗结算：持久账户的提交点
        game = current with { Session = settled, Notices = notices };
        if(next.World.Round != current.Session.World.Round || next.World.ActiveFaction != current.Session.World.ActiveFaction || next.World.Outcome != current.Session.World.Outcome
            || next.PlayerFaction != current.Session.PlayerFaction
            || flowChanged && next.World.ScenarioEvents.Count > 0)
            AutoSave();
    }
    /// <summary>选中地图格：返回移动/攻击范围、征兵、解散、技能、设施等交互提示。</summary>
    private object Selection(int cell)
    {
        var current = Require(); var world = current.Session.World;
        if (cell < 0 || cell >= world.Cells.Count) throw new RuleViolation("map.cell", "Cell outside map");
        bool ready = world.Phase == TurnPhase.Active && !flow.Blocked && world.ActiveFaction == current.Session.PlayerFaction;
        var unit = world.Units.FirstOrDefault(u => u.Cell == cell);
        var offers = ready ? current.Rules.Recruitment[world.ActiveFaction].Values.Where(o => WorldActions.CanRecruit(world, cell, o))
            .Select(o => new { o.Id, profile = o.Template?.Profile.Id, o.Cost }).ToArray() : [];
        var occupied = world.Units.Select(u => u.Cell).ToHashSet();
        var range = ready && unit?.Owner == world.ActiveFaction
            ? _engine.Reachable(world, PlayEngineRules.ToRuleSet(current.Rules), unit.Id).Keys.Where(c => !occupied.Contains(c)).ToArray() : [];
        var targets = ready && unit?.Owner == world.ActiveFaction ? AttackTargets(world, unit).Select(u => u.Cell).ToArray() : [];
        var disbandRules = DisbandConfiguration();
        var refund = ready && unit != null && WorldDisband.CanDisband(world, unit.Id, disbandRules)
            ? WorldDisband.Refund(unit, disbandRules.RecruitmentCosts[unit.Combatant.Profile.Id]) : null;
        var trainingQuote = ready && unit?.Owner == world.ActiveFaction
            ? ActionQuote(() => WorldImprovements.TrainingCost(world, unit.Id, current.Rules.Training)) : null;
        var upgradeQuote = ready ? ActionQuote(() => WorldImprovements.BuildingUpgradeCost(world, cell, current.Rules.Upgrades)) : null;
        var transportQuote = ready && unit?.Owner == world.ActiveFaction
            ? ActionQuote(() => WorldSupportActions.TransportCost(world, unit.Id, current.Rules.Transport)) : null;
        string? disbandWarning = refund != null && unit!.Combatant.Profile.Category == "fort"
            && current.Rules.Recruitment[world.ActiveFaction].Values.Any(o => o.Template?.Profile.Category == "fort"
                && o.PerTurnLimit.HasValue && world.Factions.Single(f => f.Id == world.ActiveFaction)
                    .TurnUses.GetValueOrDefault(o.TurnGroup) >= o.PerTurnLimit.Value)
            ? "解散后本回合内不能再建造要塞。" : null;
        return new { cell, range, targets, offers, disband = refund, disbandWarning,
            actionQuotes = new { train = trainingQuote, upgrade = upgradeQuote, transport = transportQuote },
            items = ready && unit?.Owner == world.ActiveFaction && unit.Combatant.ConstructionTurns == 0,
            skills = ready && unit?.Owner == world.ActiveFaction ? ActiveSkills(world, unit) : [],
            tavern = ready && world.Cells[cell].Owner == world.ActiveFaction && Service(cell) == "bar",
            shop = ready && world.Cells[cell].Owner == world.ActiveFaction && Service(cell) == "shop",
            market = ready && world.Cells[cell].Owner == world.ActiveFaction && Service(cell) == "trade",
            upgrade = ready && WorldImprovements.CanUpgradeBuilding(world, cell, current.Rules.Upgrades),
            deploy = ready && unit?.Owner == world.ActiveFaction && unit.CommanderId == null && world.Cells[cell].Owner == world.ActiveFaction,
            unit = unit?.Id, installations = ready && unit?.Owner == world.ActiveFaction ? current.Rules.Installations
                .Where(p => WorldSupportActions.CanBuildInstallation(world, unit.Id, p.Value)).Select(p => p.Key).ToArray() : [],
            undo = ready && unit != null && this.undo.Available(current.Session, unit.Id),
            transport = ready && unit?.Owner == world.ActiveFaction && WorldSupportActions.CanBuyTransport(world, unit.Id, current.Rules.Transport) };
    }
    /// <summary>枚举单位在当前外交与射程下可攻击的目标。</summary>
    private static IEnumerable<UnitState> AttackTargets(WorldState world, UnitState unit)
    {
        var map = new HexMap(world.Width, world.Height);
        return world.Units.Where(target => !WorldDiplomacy.Allied(world, target.Owner, unit.Owner)
            && AttackLegality.Check(true, true, unit.Combatant.ConstructionTurns, unit.Acted, unit.Morale.Effective,
                map.Distance(unit.Cell, target.Cell), unit.Combatant.Profile.MinimumRange, unit.Combatant.Profile.MaximumRange) == AttackBlock.None);
    }
    /// <summary>组装前端大地图状态（单位、格子、对话、战斗预览、势力外观等）。</summary>
    private object View(WorldState? previous = null, object? movementOverride = null, object? combat = null, WorldState? cellDeltaFrom = null)
    {
        var current = Require(); var session = current.Session; var world = session.World;
        var battle = content.Battle(world.ScenarioId);
        var factionColors = battle.Factions.Select((source, index) => (source, index))
            .ToDictionary(p => StaticDataScenarioInitialization.FactionId(world.ScenarioId, p.index, p.source), p => p.source.Color);
        var factionSources = battle.Factions.Select((source, index) => (source, index))
            .ToDictionary(p => StaticDataScenarioInitialization.FactionId(world.ScenarioId, p.index, p.source), p => p.source);
        var unitVariants = factionSources.ToDictionary(p => p.Key, p => p.Value.Variant);
        var countryIds = factionSources.ToDictionary(p => p.Key, p => content.Countries.Canonical(p.Value.CountryId));
        var visualVariants = factionSources.ToDictionary(p => p.Key, p => content.Country(p.Value.CountryId)?.VisualVariant
            ?? content.Country(p.Value.CountryId)?.UnitVariant ?? p.Value.Variant);
        foreach (var (id, source) in factionSources)
            unitVariants[id] = content.Country(source.CountryId)?.UnitVariant ?? source.Variant;
        foreach (var creation in ScenarioFactions.Creations(world.ScenarioEvents))
        {
            string? templateId = ScenarioFactions.Template(world, creation);
            countryIds[creation.Faction] = creation.Country ?? (templateId == null ? creation.Faction : countryIds[templateId]);
            unitVariants[creation.Faction] = creation.UnitVariant ?? (templateId == null ? "others" : unitVariants[templateId]);
            visualVariants[creation.Faction] = content.Country(countryIds[creation.Faction])?.VisualVariant
                ?? (creation.UnitVariant == null && templateId != null ? visualVariants[templateId] : unitVariants[creation.Faction]);
            if (ScenarioFactions.Template(world, creation) is not string template) continue;
            factionSources.Add(creation.Faction, factionSources[template]);
            factionColors.Add(creation.Faction, factionColors[template]);
        }
        var sites = battle.EffectiveSites.ToDictionary(s => (int)s.Cell);
        var originalUnits = battle.Armies.Select((army, index) => (army, id: world.ScenarioId + ".unit." + index))
            .ToDictionary(p => p.id, p => p.army);
        string? CommanderDefinitionId(UnitState unit) => session.Account.Roster.FirstOrDefault(c => c.Id == unit.CommanderId)?.DefinitionId
            ?? (unit.CommanderId == unit.Id + ".commander" && originalUnits.TryGetValue(unit.Id, out var army)
                && content.Catalogs.Runtime.Commanders.TryGetValue(army.Commander, out var definition) ? definition.Definition.Id : null);
        var movements = new List<object>();
        if (previous != null)
        {
            var before = previous.Units.ToDictionary(u => u.Id);
            foreach (var unit in world.Units)
                if (before.TryGetValue(unit.Id, out var old) && old.Cell != unit.Cell
                    && _engine.Reachable(previous, PlayEngineRules.ToRuleSet(current.Rules), old.Id).TryGetValue(unit.Cell, out var reachable))
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
            && !flow.Blocked && AttackTargets(world, unit).Any();
        object? DialoguePresentation()
        {
            if (PendingFlow == null || !current.Rules.Dialogues.TryGetValue(PendingFlow, out var entry)) return null;
            string? portrait = content.Catalogs.Runtime.Commanders.TryGetValue(entry.Commander, out var speaker)
                ? speaker.Definition.DisplayName : null;
            return new { id = entry.Id, text = entry.Text, left = entry.Left, portrait = portrait == null ? null : portrait + ".png",
                name = portrait == null ? "" : content.Label(portrait) };
        }
        object CellView(CellState c, int i) => new { c.Owner, c.Name, c.BuildingKind, c.BuildingLevel, c.Terrain.Active, c.Terrain.Sea, c.Objective, c.InstallationKind, c.Environment,
            objectiveMarker = c.Objective ? 1 : sites.TryGetValue(i, out var markedSite) && markedSite.Marker == 2 ? 2 : 0,
            jurisdictionCenter = world.Jurisdictions?.CellCenters[i] is int center && center >= 0 ? center : (int?)null,
            tileInfo = TileInfo(i, c),
            facility = sites.TryGetValue(i, out var service) ? service.State6 switch { 1 => "trade", 2 => "shop", 3 => "bar", _ => (string?)null } : null,
            buildingImage = BuildingImage(i, c), buildingStyle = sites.TryGetValue(i, out var site) ? site.State7 : 1 };
        return new
        {
            session.Revision, scenario = world.ScenarioId, map = content.MapNames[battle.MapId], world.Width, world.Height,
            battleResult = BattleResultView(session),
            tutorialCheckpoint,
            cameraBounds = new { left = battle.Left * 64 - 52, top = (battle.Top - 1) * 54 - 20,
                width = battle.Width * 64 + 72, height = (battle.Height - 1) * 54 + 112 },
            movements = movementOverride ?? movements,
            combat,
            recruited = previous == null ? [] : world.Units.Where(u => !previous.Units.Any(old => old.Id == u.Id)).Select(u => u.Id).ToArray(),
            undoUnit = undo.UnitFor(session),
            world.Round, phase = world.Phase.ToString(), outcome = world.Outcome.ToString(), world.ActiveFaction, session.PlayerFaction,
            startCountry = country, ending = world.ScenarioEnding, victoryRules = world.ScenarioVictory ?? current.Rules.Turns.Victory.Custom,
            localMultiplayer = LocalMultiplayerView(session),
            dialogue = PendingFlow == null ? null : current.Rules.Dialogues.TryGetValue(PendingFlow, out var dialogue) ? dialogue.Text : PendingFlow,
            dialoguePresentation = DialoguePresentation(),
            scenarioPopup = ScenarioPopupView(world),
            medals = session.Account.Medals,
            factions = world.Factions.Select(f =>
            {
                FactionAppearanceResolve.Resolved appearance = ResolveFactionAppearance(f, countryIds, factionSources, factionColors);
                return new { f.Id, f.Name, f.Camp, f.Human, f.Defeated, f.Resources,
                    relation = WorldDiplomacy.Relation(world, session.PlayerFaction, f.Id, current.Rules.Turns.NeutralCamps),
                    neutral = WorldDiplomacy.NeutralTo(world, f.Id, session.PlayerFaction, current.Rules.Turns.NeutralCamps),
                    country = countryIds.GetValueOrDefault(f.Id) ?? f.Id,
                    unitVariant = unitVariants.GetValueOrDefault(f.Id),
                    visualVariant = visualVariants.GetValueOrDefault(f.Id),
                    flag = appearance.Flag, flagImage = appearance.FlagImage, color = appearance.Color };
            }),
            cells = cellDeltaFrom == null ? world.Cells.Select(CellView).ToArray() : null,
            cellUpdates = cellDeltaFrom == null ? null : world.Cells.Select((c, i) => (c, i))
                .Where(p => p.c != cellDeltaFrom.Cells[p.i] || world.Jurisdictions?.CellCenters[p.i] != cellDeltaFrom.Jurisdictions?.CellCenters[p.i])
                .Select(p => new { cell = p.i, value = CellView(p.c, p.i) }).ToArray(),
            units = world.Units.Select(u => new { u.Id, u.Owner, u.Cell, profile = u.Combatant.Profile.Id, category = u.Combatant.Profile.Category,
                skills = SkillViews(u.Combatant), statuses = Array.Empty<PlaySkillView>(), traits = Array.Empty<PlaySkillView>(),
                health = u.Combatant.Health, maxHealth = u.Combatant.Profile.MaxHealth, u.Combatant.ConstructionTurns, u.RemainingMovement, u.Acted, u.CommanderId, u.Transport, u.Burning, u.Objective,
                objectiveMarker = u.Objective ? 1 : originalUnits.TryGetValue(u.Id, out var markedUnit) && markedUnit.Objective == 2 ? 2 : 0,
                canMove = u.Combatant.Profile.Category != "fort" && u.RemainingMovement > 0 && u.Morale.Effective != -3 && u.Combatant.ConstructionTurns == 0,
                canAttack = CanAttack(u),
                veterancy = u.Veterancy, morale = u.Morale.Effective,
                facing = originalUnits.TryGetValue(u.Id, out var source) ? source.Facing : 1,
                commanderDefinitionId = CommanderDefinitionId(u),
                commanderImage = session.Account.Roster.FirstOrDefault(c => c.Id == u.CommanderId) is OwnedCommander owned
                    ? content.Catalogs.Accounts.Rules().Definitions[owned.DefinitionId].DisplayName
                    : u.CommanderId == u.Id + ".commander" && originalUnits.TryGetValue(u.Id, out var commanded)
                    && content.Catalogs.Runtime.Commanders.TryGetValue(commanded.Commander, out var commander) ? commander.Definition.DisplayName : null }),
            notices = current.Notices,
            turnSummary = TurnSummaryView(world, session, battle, current.Notices, current.Rules.Turns),
            battleBriefing = BattleBriefingView(world, session, battle, current.Notices)
        };
    }

    private object? BattleBriefingView(WorldState world, GameSession session, StaticDataBattle battle, IReadOnlyList<WorldNotice> notices)
    {
        if (world.Round != 1 || world.Phase != TurnPhase.Active) return null;
        if (!HumanPlayerInBattle(world, session)) return null;
        var presentation = content.Presentations.GetValueOrDefault(world.ScenarioId)
            ?? content.Presentations.Values.FirstOrDefault(p => p.Alternate == world.ScenarioId);
        string title = content.Scenarios.Single(s => s.Id == world.ScenarioId).Name;
        string description = presentation == null ? "" : content.Label(presentation.Description);
        string? commander = presentation?.Commander;
        return new
        {
            title,
            description,
            commanderImage = commander == null ? null : commander + ".png",
            commanderName = commander == null ? null : content.Label("name_" + commander),
            minimumRound = battle.MinimumRound,
            ratingRound = battle.RatingRound
        };
    }

    private object? TurnSummaryView(WorldState world, GameSession session, StaticDataBattle battle, IReadOnlyList<WorldNotice> notices, WorldTurnRules rules)
    {
        if (world.Phase != TurnPhase.Active) return null;
        if (!PlayerTurnStarted(world, session, notices)) return null;
        TurnEconomySnapshot snap = WorldTurns.EconomySnapshot(world, world.ActiveFaction, rules);
        var quota = session.RosterDeployments.SingleOrDefault(q => q.Faction == world.ActiveFaction);
        return new
        {
            round = world.Round,
            income = new { money = snap.Income.Money, industry = snap.Income.Industry, food = snap.Income.Food },
            upkeep = new { food = snap.FoodUpkeep },
            shortage = snap.Shortage,
            resources = new { money = snap.Resources.Money, industry = snap.Resources.Industry, food = snap.Resources.Food },
            minimumRound = battle.MinimumRound,
            ratingRound = battle.RatingRound,
            generalSlots = quota?.Limit ?? 6,
            generals = DeployedGeneralsView(session, world.ActiveFaction)
        };
    }

    private static bool HumanPlayerInBattle(WorldState world, GameSession session)
    {
        if (session.World.LocalMultiplayer != null)
            return session.World.LocalMultiplayer.HumanFactions.Any(id => world.Factions.Any(f => f.Id == id && f.Human && !f.Defeated));
        var player = world.Factions.SingleOrDefault(f => f.Id == session.PlayerFaction);
        return player is { Human: true, Defeated: false };
    }

    private static bool PlayerTurnStarted(WorldState world, GameSession session, IReadOnlyList<WorldNotice> notices)
    {
        if (!notices.Any(n => n.Kind == "faction.started" && n.Subject == world.ActiveFaction)) return false;
        var active = world.Factions.Single(f => f.Id == world.ActiveFaction);
        if (!active.Human) return false;
        return session.World.LocalMultiplayer != null || world.ActiveFaction == session.PlayerFaction;
    }

    private object[] DeployedGeneralsView(GameSession session, string factionId)
    {
        var ids = session.RosterDeployments.Where(q => q.Faction == factionId).SelectMany(q => q.CommanderIds).ToHashSet();
        return session.Account.Roster.Where(c => ids.Contains(c.Id)).Select(c =>
        {
            var definition = content.Catalogs.Accounts.Rules().Definitions[c.DefinitionId];
            var runtime = session.World.Commanders.FirstOrDefault(g => g.Id == c.Id);
            return new
            {
                name = content.Label(definition.DisplayName),
                image = definition.DisplayName + ".png",
                rank = runtime?.Rank.Level ?? c.Learning.Rank.Level,
                title = runtime?.Title.Level ?? c.Learning.Title.Level,
                withdrawn = runtime?.Withdrawn ?? false
            };
        }).ToArray<object>();
    }

    /// <summary>解析势力旗帜、贴图与颜色（含剧本动态创建势力）。</summary>
    private FactionAppearanceResolve.Resolved ResolveFactionAppearance(
        FactionState faction,
        IReadOnlyDictionary<string, string> countryIds,
        IReadOnlyDictionary<string, StaticDataFaction> factionSources,
        IReadOnlyDictionary<string, int> factionColors)
    {
        string countryId = countryIds.GetValueOrDefault(faction.Id) ?? faction.Id;
        var country = content.Country(countryId);
        var battle = factionSources.GetValueOrDefault(faction.Id);
        return FactionAppearanceResolve.Resolve(
            faction.Appearance,
            country?.Flag,
            country?.FlagImage,
            country?.Color,
            battle?.Variant,
            factionColors.GetValueOrDefault(faction.Id),
            faction.Id);
    }

    /// <summary>计算训练/升级/运输等动作花费，非法时返回 null。</summary>
    private ClientActionQuote? ActionQuote(Func<Resources> calculate)
    {
        try
        {
            var cost = calculate();
            var world = Require().Session.World;
            var funds = world.Factions.Single(f => f.Id == world.ActiveFaction).Resources;
            return new(cost, funds.Money >= cost.Money && funds.Industry >= cost.Industry && funds.Food >= cost.Food);
        }
        catch (RuleViolation e) when (e.Code is "training.unit" or "training.unavailable" or "training.stage" or "training.limit"
            or "upgrade.site" or "upgrade.used" or "upgrade.limit" or "purchase.unit" or "purchase.site" or "transport.unavailable")
        {
            return null;
        }
    }

    /// <summary>在时间与步数上限内连续执行 AI 回合，返回最新 View 与错误信息。</summary>
    private object AiBatch()
    {
        var timer = Stopwatch.StartNew();
        var before = Require().Session;
        int steps = 0;
        object? error = null;
        try
        {
            do
            {
                var current = Require();
                var world = current.Session.World;
                var active = world.Factions.Single(f => f.Id == world.ActiveFaction);
                if (active.Human || flow.Blocked || world.Phase == TurnPhase.Finished) break;
                AiStep();
                steps++;
                if (Require().Session.World.ActiveFaction != before.World.ActiveFaction) break;
            } while (steps < 32 && timer.ElapsedMilliseconds < 250);
        }
        catch (Exception e) when (e is RuleViolation or IOException or JsonException or InvalidOperationException or ArgumentException or KeyNotFoundException or OverflowException)
        {
            // Earlier steps may already be committed; always return their latest state with the error.
            error = new { code = e is RuleViolation rule ? rule.Code : "client.failure", message = e.Message };
        }
        return new { state = View(cellDeltaFrom: steps == 0 ? null : before.World), baseRevision = before.Revision, steps, error };
    }

    /// <summary>将账号物品栏转为商店/总部 UI 用的条目列表。</summary>
    private object[] ItemViews(PlayerAccount account) => content.ItemDefinitions.Select(e =>
    {
        int id = (int)e.Attribute("id")!;
        var definition = content.Catalogs.Items.Items[id];
        string key = definition.Equipment.Id, name = (string)e.Attribute("name")!;
        long quantity = account.Inventory.GetValueOrDefault(key);
        long assigned = account.Roster.Sum(c => c.Items.LongCount(item => item == key));
        return (object)new { id = key, name = content.Label("name_" + name), image = name + ".png",
            description = content.Label("desc_" + name), definition.Price, definition.Unique,
            definition.Equipment.Consumable, quantity, available = quantity - assigned };
    }).ToArray();

    /// <summary>持久化账号；若局内存在则同步 session.Account 并 bump revision。</summary>
    private void InstallAccount(PlayerAccount account)
    {
        SetAccount(account); CommitProfile();
        if (game != null) game = game with { Session = game.Session with { Account = account, Revision = checked(game.Session.Revision + 1) } };
    }

    /// <summary>获取或刷新总部随机商店（消耗品 + 未拥有装备）。</summary>
    private ItemMarket HeadquartersMarket(bool refresh = false, long expectedRevision = -1)
    {
        var account = Account();
        if (refresh && expectedRevision != account.Revision)
            throw new RuleViolation("account.revision", "Stale account transaction");
        if (!refresh && account.HeadquartersStock != null) return account.HeadquartersStock;
        var previous = account.HeadquartersStock;
        // Native 5d434: five consumables, then two distinct unowned permanent items.
        var consumables = content.Catalogs.Items.Items.Values.Where(d => d.Equipment.Consumable).ToArray();
        var equipment = content.Catalogs.Items.Items.Values.Where(d => !d.Equipment.Consumable
            && !account.Inventory.ContainsKey(d.Equipment.Id)).ToArray();
        Random.Shared.Shuffle(equipment);
        // Prefer equipment outside the last selection when the remaining pool allows it.
        var oldEquipment = previous?.Offers.Select(o => o.ItemId).ToHashSet() ?? [];
        equipment = equipment.OrderBy(d => oldEquipment.Contains(d.Equipment.Id)).ToArray();
        var offers = new List<ItemTradeOffer>();
        for (int i = 0; i < 5 && consumables.Length > 0; i++)
        {
            string slot = "headquarters." + i;
            string? old = previous?.Offers.FirstOrDefault(o => o.Id == slot)?.ItemId;
            var choices = consumables.Length > 1 ? consumables.Where(d => d.Equipment.Id != old).ToArray() : consumables;
            offers.Add(new(slot, choices[Random.Shared.Next(choices.Length)].Equipment.Id, 1));
        }
        offers.AddRange(equipment.Take(2).Select((d, i) => new ItemTradeOffer("headquarters." + (i + 7), d.Equipment.Id)));
        var market = new ItemMarket("headquarters", offers);
        InstallAccount(account.Copy() with { Revision = checked(account.Revision + 1), HeadquartersStock = market });
        return market;
    }

    /// <summary>总部或地图商店/酒馆：浏览、刷新、买卖物品。</summary>
    private object Commerce(PlayCommand command)
    {
        bool headquarters = command.Cell < 0;
        if (command.Op == "refresh" && !headquarters)
            throw new RuleViolation("trading.command", "Only the headquarters shop can refresh its stock");
        ItemMarket market;
        int commerce = 0;
        ItemTradingRules trading;
        if (headquarters)
        {
            market = HeadquartersMarket(command.Op == "refresh", command.Revision);
            trading = content.Catalogs.Items.Rules();
        }
        else
        {
            var current = Require(); var world = current.Session.World;
            ServiceAccess(command.Cell, command.Op == "tavern" ? "bar" : "shop");
            if (command.Op == "tavern") return TavernView(command.Cell);
            var service = current.Session.Markets.SingleOrDefault(m => m.Cell == command.Cell)
                ?? throw new RuleViolation("session.market_access", "No item shop exists at this location");
            market = service.Market;
            commerce = SessionAccountActions.CommerceLevel(current.Session, service, TradingRules());
            trading = TradingRules().Items;
        }
        if (command.Op is "buy" or "sell")
        {
            if (command.Op == "buy")
            {
                CommitAccountChange(headquarters
                    ? SessionAccountActions.BuyHeadquarters(Require().Session, command.Revision, command.Offer!, trading)
                    : SessionAccountActions.BuyItem(Require().Session, command.Revision, market.Id, command.Offer!, TradingRules()), headquarters);
            }
            else
            {
                CommitAccountChange(headquarters
                    ? SessionAccountActions.SellHeadquarters(Require().Session, command.Revision, command.Item!, trading)
                    : SessionAccountActions.SellItem(Require().Session, command.Revision, market.Id, command.Item!, TradingRules()), headquarters);
            }

            market = headquarters
                ? Account().HeadquartersStock ?? market
                : Require().Session.Markets.Single(m => m.Market.Id == market.Id).Market;
        }
        else if (command.Op is not ("" or "refresh")) throw new RuleViolation("trading.command", "Unknown shop operation");
        var owned = Account();
        return new { revision = headquarters ? owned.Revision : Require().Session.Revision, owned.Medals, commerce,
            items = ItemViews(owned), offers = market.Offers.Select(o => new { o.Id, o.ItemId, o.Remaining,
                slot = int.Parse(o.Id.Split('.').Last()),
                price = ItemTrading.PurchasePrice(trading.Definitions[o.ItemId].Price, commerce, trading).Medals,
                available = ItemTrading.Available(owned, o, trading) }),
            sales = owned.Inventory.Keys.Select(id => new { id, price = ItemTrading.SalePrice(trading.Definitions[id].Price, commerce, trading).Medals }) };
    }

    /// <summary>指定格酒馆：可雇佣的将领候选与花费。</summary>
    private object TavernView(int cell)
    {
        var current = Require(); var session = current.Session; var world = session.World;
        var unit = world.Units.FirstOrDefault(u => u.Cell == cell && u.Owner == session.PlayerFaction && u.CommanderId == null);
        var sources = content.Battle(world.ScenarioId).Taverns.SelectMany(t => t.Candidates).ToArray();
        var resources = world.Factions.Single(f => f.Id == session.PlayerFaction).Resources;
        return new { session.Revision, world.Round, session.Account.Medals, unit = unit?.Id,
            candidates = session.TavernCandidates.Where(c => c.Cell == cell).Select(c =>
            {
                int index = int.Parse(c.Id.Split('.').Last());
                return new { c.Id, definitionId = content.Catalogs.Runtime.Commanders[sources[index].CommanderId].Definition.Id,
                    c.Cost, c.Medals, c.AvailableRound,
                    available = unit != null && c.AvailableRound <= world.Round && session.Account.Medals >= c.Medals
                        && resources.Money >= c.Cost.Money && resources.Industry >= c.Cost.Industry };
            }) };
    }

    /// <summary>指定己方部队可使用的消耗品及不可用原因。</summary>
    private object Consumables(PlayCommand command)
    {
        var current = Require(); var session = current.Session;
        var unit = session.World.Units.SingleOrDefault(u => u.Id == command.Unit);
        if (unit == null || unit.Owner != session.PlayerFaction)
            throw new RuleViolation("item.target", "请选择己方部队");
        return new { session.Revision, unit = unit.Id, items = content.ItemDefinitions.Select(e =>
        {
            var item = content.Catalogs.Items.Items[(int)e.Attribute("id")!].Equipment;
            string name = (string)e.Attribute("name")!;
            string? reason = flow.Blocked ? "当前无法使用物品" : ConsumableItems.UnavailableReason(session, unit.Id, item, current.Rules.Training);
            return new { id = item.Id, name = content.Label("name_" + name), image = name + ".png",
                description = content.Label("desc_" + name), item.Consumable,
                quantity = session.Account.Inventory.GetValueOrDefault(item.Id), available = reason == null, reason };
        }).Where(i => i.Consumable).ToArray() };
    }

    /// <summary>指定部队可挂载的将领列表与部署配额使用情况。</summary>
    private object Deployment(PlayCommand command)
    {
        var current = Require();
        if (command.Revision != current.Session.Revision) throw new RuleViolation("client.stale", "State changed; refresh the command");
        var quota = current.Session.RosterDeployments.SingleOrDefault(q => q.Faction == current.Session.PlayerFaction);
        return new { current.Session.Revision, limit = quota?.Limit, used = quota?.CommanderIds.Count ?? 0,
            commanders = flow.Blocked ? [] : current.Session.Account.Roster.Where(c => RosterDeployment.CanDeploy(current.Session, command.Unit!, c.Id, current.Rules.Roster))
                .Select(c => c.Id).ToArray() };
    }

    /// <summary>征服模式下解散部队的规则与各兵种征兵退款表。</summary>
    private DisbandRules DisbandConfiguration()
    {
        var current = Require();
        bool conquest = content.Scenarios.Single(s => s.Id == current.Session.World.ScenarioId).Mode == 2;
        var offers = current.Rules.Recruitment[current.Session.PlayerFaction];
        return new() { Enabled = conquest, PlayerFaction = current.Session.PlayerFaction,
            RecruitmentCosts = offers.Values.Where(o => o.Template != null).GroupBy(o => o.Template!.Profile.Id)
                .ToDictionary(g => g.Key, g => g.First().Cost, StringComparer.Ordinal) };
    }

    private readonly IEngine _engine = engine;

    /// <summary>构造 IEngine.Apply 所需的提交上下文与规则覆盖。</summary>
    private EngineApplyContext BuildEngineContext()
    {
        var current = Require();
        return new EngineApplyContext
        {
            Commit = new WorldCommitContext(
                current.Session.World,
                current.Rules.Turns),
            DisbandOverride = DisbandConfiguration(),
        };
    }

    /// <summary>执行引擎命令并将结果写回当前局。</summary>
    private EngineResult ApplyEngine(EngineCommand command, bool replaceFlow = false)
    {
        var current = Require();
        EngineResult result = _engine.Apply(current.Session, PlayEngineRules.ToRuleSet(current.Rules), command, BuildEngineContext());
        var before = flow.Waiting;
        if (replaceFlow) flow.Replace(result.Flow); else flow.Raise(result.Flow);
        InstallSession(current, result.Session, result.Notices, !before.SequenceEqual(flow.Waiting));
        return result;
    }

    /// <summary>提交账户侧事务（商店等）；这类事务不经引擎。总部操作立即落盘，战场内商店只改内存，战斗结算时再落盘。</summary>
    private void CommitAccountChange(SessionChange change, bool commitProfile)
    {
        CommitSession(change);
        if (commitProfile) CommitProfile();
    }

    /// <summary>内容修订后，按新旧将领定义差量修正已存档单位属性。</summary>
    private static GameSession UpgradeEquipment(GameSession state, StaticDataScenarioStart prior, StaticDataScenarioStart current)
    {
        Dictionary<string, Commander> Definitions(RosterDeploymentRules roster, WorldCommanderRules scenario)
        {
            var definitions = scenario.Definitions.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            foreach (var owned in state.Account.Roster) definitions[owned.Id] = AccountCommanders.Resolve(owned, roster.Account);
            return definitions;
        }
        var oldDefinitions = Definitions(prior.Rules.Roster, prior.Rules.Commanders);
        var definitions = Definitions(current.Rules.Roster, current.Rules.Commanders);
        var units = state.World.Units.Select(unit =>
        {
            if (unit.CommanderId == null) return unit;
            if (!oldDefinitions.TryGetValue(unit.CommanderId, out var oldDefinition)
                || !definitions.TryGetValue(unit.CommanderId, out var definition))
                throw new RuleViolation("client.equipment_migration", "Cannot resolve saved commander's equipment");
            var old = Ew4Commander.Resolve(oldDefinition, unit.Combatant.Profile.Category, prior.Rules.Commanders.SkillParameters);
            var effect = Ew4Commander.Resolve(definition, unit.Combatant.Profile.Category, current.Rules.Commanders.SkillParameters);
            int movement = unit.Combatant.Profile.Category == "fort" ? 0 : effect.MovementBonus - old.MovementBonus;
            return unit with
            {
                Combatant = unit.Combatant with
                {
                    AttackBonus = checked(unit.Combatant.AttackBonus + effect.AttackEquipment - old.AttackEquipment),
                    DefenseBonus = checked(unit.Combatant.DefenseBonus + effect.DefenseEquipment - old.DefenseEquipment),
                    Profile = unit.Combatant.Profile with { Movement = checked(unit.Combatant.Profile.Movement + movement) }
                },
                RemainingMovement = unit.RemainingMovement == 0 ? 0 : Math.Max(0, checked(unit.RemainingMovement + movement)),
                ExtensionMovementLimit = unit.ExtensionMovementLimit.HasValue ? checked(unit.ExtensionMovementLimit.Value + movement) : null,
                Support = unit.Support with
                {
                    FieldHealing = checked(unit.Support.FieldHealing + effect.FieldHealing - old.FieldHealing),
                    AdjacentHealing = checked(unit.Support.AdjacentHealing + effect.AdjacentHealing - old.AdjacentHealing),
                    AdjacentAttack = checked(unit.Support.AdjacentAttack + effect.AdjacentAttack - old.AdjacentAttack),
                    AdjacentDefense = checked(unit.Support.AdjacentDefense + effect.AdjacentDefense - old.AdjacentDefense)
                }
            };
        }).ToArray();
        int? Percent(Commander definition, int effect) => definition.Items.Where(i => i.Effect == Ew4Commander.Effect(effect))
            .Select(i => (int?)i.Value).FirstOrDefault();
        var commanders = state.World.Commanders.Select(commander => !definitions.TryGetValue(commander.Id, out var definition) ? commander
            : commander with { RankExperiencePercent = Percent(definition, 1), TitleExperiencePercent = Percent(definition, 2) }).ToArray();
        var next = state with
        {
            ContentRevision = current.Session.ContentRevision,
            World = state.World with { ContentRevision = current.Session.World.ContentRevision, Units = units, Commanders = commanders }
        };
        next.Validate();
        return next;
    }

    /// <summary>单个技能槽 → 本地化名称、图标与消耗的 PlaySkillView。</summary>
    private PlaySkillView SkillView(SkillSlot slot)
    {
        int number = slot.Id.StartsWith("ew4.skill.", StringComparison.Ordinal) && int.TryParse(slot.Id[10..], out int id) ? id + 1 : 0;
        return new(slot.Id, slot.InstanceId, slot.Level, slot.Order, slot.X, slot.Y,
            content.Label(number > 0 ? "name_skill_" + number.ToString("00") : slot.Id),
            number > 0 ? content.Label("desc_skill_" + number.ToString("00")) : "",
            number > 0 ? "skill_" + number.ToString("00") + ".png" : null, "passive", "self", 0, new(0, 0, 0));
    }
    /// <summary>单位身上全部技能槽的展示列表。</summary>
    private PlaySkillView[] SkillViews(Combatant unit) => CombatantSkills.Slots(unit).OrderBy(s => s.Order).Select(SkillView).ToArray();
    /// <summary>当前回合可选主动技能（仅引擎原版技能；扩展主动技已移除）。</summary>
    private object[] ActiveSkills(WorldState world, UnitState unit) => [];

    private const int CommanderSectionCapacity = 20;
    private ProfileStore? profileStore;
    private ProfileStore profile => profileStore ??= new(Profile, Json);
    /// <summary>本地玩家账号文件 account.json 路径。</summary>
    private string Profile => SaveDirectoryLayout.AccountPath(saveDirectory);
    /// <summary>懒加载账号：读盘、默认资源、招募池与 content revision 迁移。</summary>
    private PlayerAccount Account()
    {
        if (profile.Current is { } cached) return cached;
        string accountContentRevision = AccountContentRevision();
        PlayerAccount account;
        if (profile.Exists)
        {
            account = profile.Open();
        }
        else
        {
            account = new PlayerAccount { Id = "local", ContentRevision = accountContentRevision, Medals = 20_000_000, Badges = 20_000_000 };
        }
        if (account.CommanderOffers.Count == 0)
            for (int group = 0; group < 3; group++) account = DrawCommanders(account, group);
        if (account.ContentRevision != accountContentRevision)
        {
            account = new PlayerAccount
            {
                Id = account.Id,
                ContentRevision = accountContentRevision,
                Medals = account.Medals,
                Badges = account.Badges,
                UnlockedCampaigns = account.UnlockedCampaigns,
            };
            for (int group = 0; group < 3; group++)
            {
                account = DrawCommanders(account, group);
            }

            profile.Reset(account); profile.MarkDirty(); CommitProfile();
        }

        profile.Reset(account);
        account.Validate();
        foreach (var owned in account.Roster) AccountCommanders.Resolve(owned, content.Catalogs.Accounts.Rules());
        return account;
    }
    // 持久账户的内存工作副本与提交点见 ProfileStore。
    private void SetAccount(PlayerAccount next) => profile.Set(next);
    private void CommitProfile() => profile.Commit();
    /// <summary>离开当前局（退出、或被新开局/读档替换）：战役与征服提交账户，其他模式丢弃。</summary>
    private void EndBattleProfile()
    {
        if (game == null) return;
        profile.Leave(content.Scenarios.Single(s => s.Id == game.Session.World.ScenarioId).Mode);
    }
    /// <summary>本局已部署或仍在战场上的将领 id（总部不可再操作）。</summary>
    private HashSet<string> LockedCommanders() => game == null ? [] : game.Session.RosterDeployments
        .SelectMany(q => q.CommanderIds).Concat(game.Session.World.Commanders.Select(c => c.Id)).ToHashSet(StringComparer.Ordinal);
    /// <summary>总部界面数据：名册、招募池、公主、物品与将领定义表。</summary>
    private object Headquarters()
    {
        var account = Account(); var locked = LockedCommanders();
        var sources = content.Catalogs.Runtime.SourceTrees.GetProperty("def_commander.xml").GetProperty("children");
        return new { account.Revision, account.Medals, account.Badges, sectionCapacity = CommanderSectionCapacity, offers = account.CommanderOffers, roster = account.Roster.Select(c => new { c.Id, c.DefinitionId, c.Learning, c.Training, c.Items, c.Tag,
            locked = locked.Contains(c.Id), rankPrice = PromotionPrice(c, false), titlePrice = PromotionPrice(c, true), fullPrice = FullPromotionPrice(c),
            healthBonus = content.Catalogs.Combat.RankHealth[c.Learning.Rank.Level.ToString(CultureInfo.InvariantCulture)],
            healing = content.Catalogs.Combat.TitleHealing[c.Learning.Title.Level.ToString(CultureInfo.InvariantCulture)] }),
            rankHealth = content.Catalogs.Combat.RankHealth, titleHealing = content.Catalogs.Combat.TitleHealing,
            progression = new { rank = content.Catalogs.Combat.RankProgression.Levels.Select(l => l.ExperienceToNext),
                title = content.Catalogs.Combat.TitleProgression.Levels.Select(l => l.ExperienceToNext) },
            definitions = sources.EnumerateArray().Select(e =>
            {
                var a = e.GetProperty("attributes");
                string Text(string key, string fallback = "") => a.TryGetProperty(key, out var v) ? v.GetString()! : fallback;
                int Number(string key) => int.Parse(Text(key, "0"), CultureInfo.InvariantCulture);
                int id = Number("id"); var source = content.Catalogs.Runtime.Commanders[id];
                return new { id = source.Definition.Id, legacyId = id, name = content.Label(Text("name")), image = Text("image", Text("name") + ".png"),
                    country = Text("country"), rating = Number("star"), rank = source.Rank, title = source.Title, source.Training,
                    price = Number("price"), badgePrice = BadgePrice(Number("star")), recruitable = Number("drawlots") != 0,
                    healthBonus = content.Catalogs.Combat.RankHealth[source.Rank.ToString(CultureInfo.InvariantCulture)],
                    healing = content.Catalogs.Combat.TitleHealing[source.Title.ToString(CultureInfo.InvariantCulture)],
                    skills = (source.Definition.SkillSlots.Count > 0 ? source.Definition.SkillSlots
                        : source.Definition.Skills.Select((s, i) => new SkillSlot(s, "legacy." + i)).ToArray()).OrderBy(s => s.Order).Select(SkillView),
                    abilities = content.Catalogs.Accounts.PreviewAbilities(id) };
            }),
            items = ItemViews(account), equipmentCapacity = content.Catalogs.Accounts.Rules().EquipmentCapacity,
            princesses = content.PrincessDefinitions.Select(p => new { id = "princess." + (string)p.Attribute("id")!,
                definitionId = "ew4.commander." + (string)p.Attribute("commanderid")!, image = (string)p.Attribute("name")! + ".png",
                unlocked = true, claimed = account.Roster.Any(c => c.DefinitionId == "ew4.commander." + (string)p.Attribute("commanderid")!) }) };
    }
    // Original 60030/195150: three offer pools, distinct from individual ability stars.
    private static readonly int[] BadgePrices = [0, 0, 0, 3, 3, 4, 6, 8];
    /// <summary>按将领星级返回徽章招募价格。</summary>
    private static int BadgePrice(int rating) => rating > 0 && rating <= BadgePrices.Length ? BadgePrices[rating - 1] : 0;
    /// <summary>将领定义所属三星区索引（0–2）。</summary>
    private int CommanderSection(string definitionId)
    {
        var source = content.Catalogs.Runtime.SourceTrees.GetProperty("def_commander.xml").GetProperty("children").EnumerateArray()
            .Select(e => e.GetProperty("attributes")).Single(a => "ew4.commander." + a.GetProperty("id").GetString() == definitionId);
        return Math.Clamp((int.Parse(source.GetProperty("star").GetString()!, CultureInfo.InvariantCulture) - 1) / 3, 0, 2);
    }
    /// <summary>招募前检查对应星区名册是否已达 20 人上限。</summary>
    private void RequireCommanderSlot(PlayerAccount account, string definitionId)
    {
        int section = CommanderSection(definitionId);
        if (account.Roster.Count(c => CommanderSection(c.DefinitionId) == section) >= CommanderSectionCapacity)
            throw new RuleViolation("account.capacity", "该星区已满（20位将领）");
    }
    /// <summary>为指定星区重新随机可招募将领 id 列表。</summary>
    private PlayerAccount DrawCommanders(PlayerAccount account, int group)
    {
        if (group is < 0 or > 2) throw new RuleViolation("account.offer_group", "Unknown original recruitment section");
        int[] counts = [6, 4, 2];
        var excluded = account.Roster.Select(c => c.DefinitionId).Concat(account.CommanderOffers.Values.SelectMany(v => v)).ToHashSet(StringComparer.Ordinal);
        var pool = content.Catalogs.Runtime.SourceTrees.GetProperty("def_commander.xml").GetProperty("children").EnumerateArray()
            .Select(e => e.GetProperty("attributes")).Where(a => a.GetProperty("drawlots").GetString() == "1"
                && int.Parse(a.GetProperty("star").GetString()!, CultureInfo.InvariantCulture) is int rating && rating >= group * 3 + 1 && rating <= group * 3 + 3)
            .Select(a => "ew4.commander." + a.GetProperty("id").GetString()).Where(id => !excluded.Contains(id)).ToArray();
        Random.Shared.Shuffle(pool);
        var offers = account.CommanderOffers.ToDictionary(p => p.Key, p => p.Value);
        offers[group] = pool.Take(counts[group]).ToArray();
        return account with { CommanderOffers = offers };
    }
    /// <summary>将军衔或称号升到下一档所需的勋章数。</summary>
    private long? PromotionPrice(OwnedCommander owned, bool title)
    {
        var track = title ? content.Catalogs.Combat.TitleProgression : content.Catalogs.Combat.RankProgression;
        var current = title ? owned.Learning.Title : owned.Learning.Rank;
        if (current.Level < 0 || current.Level >= track.Levels.Count) throw new RuleViolation("account.progression", "Unknown progression stage");
        if (track.Levels[current.Level].ExperienceToNext is not long threshold) return null;
        // Original 56444/56650: remaining XP times an IEEE single-precision medal rate.
        return (long)Math.Ceiling((float)Math.Max(0, threshold - current.Experience) * (title ? .2f : .008f));
    }
    /// <summary>一键满级两条成长线的勋章总价（上限 3600）。</summary>
    private long FullPromotionPrice(OwnedCommander owned)
    {
        long Remaining(ProgressionState state, ProgressionRules track) => Math.Max(0,
            track.Levels.Skip(state.Level).Sum(level => level.ExperienceToNext ?? 0) - state.Experience);
        // Native 56754: sum remaining XP, round each track, cap at 3600 medals.
        return Math.Min(3600, (long)Math.Ceiling((float)Remaining(owned.Learning.Rank, content.Catalogs.Combat.RankProgression) * .008f)
            + (long)Math.Ceiling((float)Remaining(owned.Learning.Title, content.Catalogs.Combat.TitleProgression) * .2f));
    }
    /// <summary>总部将领命令：招募、刷新池、培养、装备、传承、标签等。</summary>
    private object CommanderAction(PlayCommand command)
    {
        var account = Account(); var rules = content.Catalogs.Accounts.Rules();
        if (command.Revision != account.Revision) throw new RuleViolation("account.revision", "Roster changed; refresh this command");
        PlayerAccount next;
        if (command.Op == "claim")
        {
            var reward = content.PrincessDefinitions.SingleOrDefault(p => "princess." + (string)p.Attribute("id")! == command.Offer)
                ?? throw new RuleViolation("account.reward", "Unknown princess reward");
            int id = (int)reward.Attribute("commanderid")!;
            if (account.Roster.Any(c => c.DefinitionId == "ew4.commander." + id))
                throw new RuleViolation("account.reward_claimed", "Princess already claimed");
            if (account.Roster.Count >= rules.MaximumRoster) throw new RuleViolation("account.capacity", "Roster is full");
            var owned = content.Catalogs.Accounts.FromDefinition(id, "local.commander." + id + "." + Guid.NewGuid().ToString("N"));
            RequireCommanderSlot(account, owned.DefinitionId);
            var inventory = account.Inventory.ToDictionary(p => p.Key, p => p.Value);
            foreach (string item in owned.Items) inventory[item] = checked(inventory.GetValueOrDefault(item) + 1);
            next = account.Copy() with { Revision = checked(account.Revision + 1), Roster = account.Roster.Append(owned).ToArray(), Inventory = inventory,
                ClaimedRewards = account.ClaimedRewards.Append(command.Offer!).Distinct(StringComparer.Ordinal).ToArray() };
        }
        else if (command.Op == "preview_inherit")
        {
            var donor = account.Roster.Single(c => c.Id == command.Target);
            var receiver = account.Roster.Single(c => c.Id == command.Commander);
            var preview = AccountCommanders.Inherit(account, account.Revision, donor.Id, receiver.Id, LockedCommanders(), rules);
            return new { learning = preview.Roster.Single(c => c.Id == receiver.Id).Learning, price = rules.InheritanceMedals };
        }
        else if (command.Op == "refresh")
            next = DrawCommanders(account, command.Group) with { Revision = checked(account.Revision + 1) };
        else if (command.Op is "recruit" or "recruit_badge")
        {
            var source = content.Catalogs.Runtime.SourceTrees.GetProperty("def_commander.xml").GetProperty("children").EnumerateArray()
                .Select(e => e.GetProperty("attributes")).FirstOrDefault(a => "ew4.commander." + a.GetProperty("id").GetString() == command.Commander);
            bool badge = command.Op == "recruit_badge";
            long price = source.ValueKind == JsonValueKind.Undefined ? 0 : badge ? BadgePrice(int.Parse(source.GetProperty("star").GetString()!, CultureInfo.InvariantCulture))
                : source.TryGetProperty("price", out var rawPrice) && long.TryParse(rawPrice.GetString(), out long medals) ? medals : 0;
            if (source.ValueKind == JsonValueKind.Undefined || source.GetProperty("drawlots").GetString() != "1" || price <= 0
                || !account.CommanderOffers.TryGetValue(command.Group, out var offers) || !offers.Contains(command.Commander!)
                || account.Roster.Any(c => c.DefinitionId == command.Commander))
                throw new RuleViolation("account.recruit", "Commander has no available medal offer");
            if ((badge ? account.Badges : account.Medals) < price) throw new RuleViolation("account.medals", "Insufficient recruitment currency");
            int legacyId = int.Parse(source.GetProperty("id").GetString()!, CultureInfo.InvariantCulture);
            var owned = content.Catalogs.Accounts.FromDefinition(legacyId, "local.commander." + legacyId);
            RequireCommanderSlot(account, owned.DefinitionId);
            var inventory = account.Inventory.ToDictionary(p => p.Key, p => p.Value);
            foreach (string item in owned.Items) inventory[item] = checked(inventory.GetValueOrDefault(item) + 1);
            next = account with { Revision = checked(account.Revision + 1), Medals = account.Medals - (badge ? 0 : price), Badges = account.Badges - (badge ? price : 0),
                Roster = account.Roster.Append(owned).ToArray(), Inventory = inventory,
                CommanderOffers = account.CommanderOffers.ToDictionary(p => p.Key, p => p.Key == command.Group ? (IReadOnlyList<string>)p.Value.Where(id => id != command.Commander).ToArray() : p.Value) };
        }
        else if (command.Op == "tags")
        {
            var ids = command.TaggedCommanders ?? throw new RuleViolation("account.tag", "Missing commander tag selection");
            var tagged = ids.ToHashSet(StringComparer.Ordinal);
            if (tagged.Count != ids.Count || tagged.Any(id => !account.Roster.Any(c => c.Id == id)))
                throw new RuleViolation("account.tag", "Tag selection must contain distinct owned commanders");
            next = account.Copy() with { Revision = checked(account.Revision + 1),
                Roster = account.Roster.Select(c => c with { Tag = tagged.Contains(c.Id) ? string.IsNullOrEmpty(c.Tag) ? "置顶" : c.Tag : "" }).ToArray() };
        }
        else if (command.Op == "tag")
        {
            var owned = account.Roster.SingleOrDefault(c => c.Id == command.Commander)
                ?? throw new RuleViolation("account.commander", "Commander is not owned");
            string tag = (command.Tag ?? "").Trim();
            if (tag.Length > 12 || tag.Any(char.IsControl))
                throw new RuleViolation("account.tag", "标签最多12个字，不能含换行或控制字符");
            next = account.Copy() with { Revision = checked(account.Revision + 1),
                Roster = account.Roster.Select(c => c.Id == owned.Id ? c with { Tag = tag } : c).ToArray() };
        }
        else
        {
            var owned = account.Roster.SingleOrDefault(c => c.Id == command.Commander)
                ?? throw new RuleViolation("account.commander", "Commander is not owned");
            if (LockedCommanders().Contains(owned.Id)) throw new RuleViolation("account.deployed", "Commander is deployed in the current game");
            if (command.Op == "full")
            {
                long price = FullPromotionPrice(owned);
                if (price > account.Medals) throw new RuleViolation("account.medals", "Insufficient medals");
                var learning = owned.Learning with { Rank = new(content.Catalogs.Combat.RankProgression.Levels.Count - 1, 0),
                    Title = new(content.Catalogs.Combat.TitleProgression.Levels.Count - 1, 0) };
                next = account.Copy() with { Revision = checked(account.Revision + 1), Medals = account.Medals - price,
                    Roster = account.Roster.Select(c => c.Id == owned.Id ? c with { Learning = learning } : c).ToArray() };
            }
            else if (command.Op is "rank" or "title")
            {
                bool title = command.Op == "title";
                long price = PromotionPrice(owned, title) ?? throw new RuleViolation("account.cap", "Commander already reached the configured final stage");
                if (price > account.Medals) throw new RuleViolation("account.medals", "Insufficient medals");
                var current = title ? owned.Learning.Title : owned.Learning.Rank;
                var promoted = new ProgressionState(current.Level + 1, 0);
                var updated = owned with { Learning = title ? owned.Learning with { Title = promoted } : owned.Learning with { Rank = promoted } };
                next = account.Copy() with { Revision = checked(account.Revision + 1), Medals = account.Medals - price,
                    Roster = account.Roster.Select(c => c.Id == owned.Id ? updated : c).ToArray() };
            }
            else next = command.Op switch {
                "equip" => AccountCommanders.Equip(account, account.Revision, owned.Id, command.Item!, rules),
                "unequip" => AccountCommanders.Unequip(account, account.Revision, owned.Id, command.Item!, rules),
                "inherit" => AccountCommanders.Inherit(account, account.Revision, command.Target!, owned.Id, LockedCommanders(), rules),
                _ => throw new RuleViolation("account.command", "Unknown commander command") };
        }
        SetAccount(next); CommitProfile();
        if (game != null) game = game with { Session = game.Session with { Account = next, Revision = checked(game.Session.Revision + 1) } };
        return Headquarters();
    }

    /// <summary>账号存档绑定的内容修订号（含 mod id 前缀）。</summary>
    private string AccountContentRevision()
    {
        string modId = slotEnvelope?.ModId ?? "";
        return string.IsNullOrEmpty(modId)
            ? content.Catalogs.Accounts.SourceRevision
            : modId + ":" + content.Catalogs.Accounts.SourceRevision;
    }

    /// <summary>开局前：本地多人模式下可选的人类势力列表。</summary>
    private object LocalPlayers(PlayCommand command)
    {
        var scenario = content.Scenarios.Single(s => s.Id == command.Scenario);
        if (scenario.Mode is not (1 or 2)) throw new RuleViolation("local.mode", "Local multiplayer requires a campaign or conquest");
        if (!CampaignProgress.Accessible(content, Account(), scenario.Id)) throw new RuleViolation("campaign.locked", "Campaign is locked");
        var battle = content.Battle(scenario.Id);
        int primary = scenario.Mode == 2 ? battle.Factions.ToList().FindIndex(f => content.Countries.Canonical(f.CountryId) == (command.Country == null ? null : content.Countries.Canonical(command.Country)))
            : battle.Factions.ToList().FindIndex(f => !f.Ai);
        if (primary < 0 || scenario.Mode == 2 && battle.Factions[primary].Camp == 4)
            throw new RuleViolation("local.selection", "Choose a playable initial country first");
        var order = new[] { primary }.Concat(Enumerable.Range(0, battle.Factions.Count).Where(i => i != primary));
        return order.Where(i => scenario.Mode != 2 || battle.Factions[i].Camp != 4)
            .Select(i => new { id = StaticDataScenarioInitialization.FactionId(scenario.Id, i, battle.Factions[i]),
            name = content.CountryName(battle.Factions[i].CountryId), country = content.Countries.Canonical(battle.Factions[i].CountryId),
            flag = content.Country(battle.Factions[i].CountryId)?.Flag ?? battle.Factions[i].Variant,
            flagImage = content.Country(battle.Factions[i].CountryId)?.FlagImage, locked = i == primary }).ToArray();
    }

    /// <summary>征服模式开局校验：禁止选择中立营作为本地玩家。</summary>
    private void ValidateLocalSelection(PlayCommand command)
    {
        if (command.LocalPlayers == null || content.Scenarios.Single(s => s.Id == command.Scenario).Mode != 2) return;
        var battle = content.Battle(command.Scenario!);
        if (battle.Factions.Select((f, i) => (f, i)).Any(p => p.f.Camp == 4
            && command.LocalPlayers.Contains(StaticDataScenarioInitialization.FactionId(command.Scenario!, p.i, p.f))))
            throw new RuleViolation("local.selection", "Neutral conquest countries cannot be selected as local players");
    }

    /// <summary>本地多人模式下胜负与各方结果摘要。</summary>
    private object? LocalMultiplayerView(GameSession session)
    {
        var local = session.World.LocalMultiplayer;
        if (local == null) return null;
        return new { local.MainFaction, local.HumanFactions, local.WinnerFaction,
            results = local.WinnerFaction == null ? null : session.World.Factions.Select(f => new {
                f.Id, f.Name, outcome = f.Id == local.WinnerFaction ? "Victory" : "Defeat" }).ToArray() };
    }

    /// <summary>地图格上的设施类型：交易所/商店/酒馆。</summary>
    private string? Service(int cell) => content.Battle(Require().Session.World.ScenarioId).Sites
        .FirstOrDefault(s => s.Cell == cell)?.State6 switch { 1 => "trade", 2 => "shop", 3 => "bar", _ => null };

    /// <summary>校验玩家在自己回合能访问己方格子的指定服务建筑。</summary>
    private void ServiceAccess(int cell, string service)
    {
        var current = Require(); var world = current.Session.World;
        var active = world.Factions.Single(f => f.Id == world.ActiveFaction);
        if (cell < 0 || cell >= world.Cells.Count || world.Phase != TurnPhase.Active || flow.Blocked
            || active.Id != current.Session.PlayerFaction || !active.Human || !world.Cells[cell].Terrain.Active
            || world.Cells[cell].BuildingKind == null || world.Cells[cell].Owner != active.Id || Service(cell) != service)
            throw new RuleViolation("session.market_access", "Select an owned service location during your turn");
    }

    // Initial-army commanders are absent from the tavern import. Extend only the
    // client lookup so existing scenario/save content fingerprints stay stable.
    /// <summary>当前局交易规则，并补上开局军队将领的商业等级映射。</summary>
    private SessionTradingRules TradingRules()
    {
        var current = Require();
        var commerce = current.Rules.Trading.CommanderCommerce.ToDictionary(p => p.Key, p => p.Value);
        foreach (var (army, index) in content.Battle(current.Session.World.ScenarioId).Armies.Select((a, i) => (a, i)))
            if (content.Catalogs.Accounts.CommanderCommerce.TryGetValue(army.Commander, out int level))
                commerce.TryAdd(current.Session.World.ScenarioId + ".unit." + index + ".commander", level);
        return current.Rules.Trading with { CommanderCommerce = commerce };
    }

    /// <summary>交易所：工业/食品与金钱互换及买卖执行。</summary>
    private object Exchange(PlayCommand command)
    {
        ServiceAccess(command.Cell, "trade");
        var current = Require(); var session = current.Session; var world = session.World;
        int commerce = SessionAccountActions.CommerceLevel(session, new(command.Cell, new("exchange", [])), TradingRules());
        float rate = .4f * (5 - Math.Min(5, commerce)) + 1f;
        // Native 5a0f8/5a39c and tables 194ef0..194f20, IEEE single precision.
        int[] amounts = [2, 10, 50, 250], money = [10, 50, 10, 50];
        if (command.Op is "buy" or "sell")
        {
            if (command.Revision != session.Revision) throw new RuleViolation("client.stale", "State changed; refresh the command");
            if (!int.TryParse(command.Offer, out int index) || index < 0 || index >= amounts.Length)
                throw new RuleViolation("exchange.offer", "Unknown resource exchange offer");
            bool buy = command.Op == "buy", industry = index < 2;
            long quantity = buy ? amounts[index] : (int)(amounts[index] * rate);
            long payment = buy ? (int)(money[index] * rate) : money[index];
            var faction = world.Factions.Single(f => f.Id == session.PlayerFaction); var funds = faction.Resources;
            if (buy ? funds.Money < payment : (industry ? funds.Industry : funds.Food) < quantity)
                throw new RuleViolation("exchange.resources", "Insufficient resources");
            long delta = buy ? quantity : -quantity;
            var updated = new Resources(checked(funds.Money + (buy ? -payment : payment)),
                checked(funds.Industry + (industry ? delta : 0)), checked(funds.Food + (!industry ? delta : 0)));
            var next = world with { Revision = checked(world.Revision + 1),
                Factions = world.Factions.Select(f => f.Id == faction.Id ? f with { Resources = updated } : f).ToArray() };
            next.Validate(); Commit(new(next, [new WorldNotice("resources.exchanged", faction.Id, command.Cell)]));
            session = Require().Session;
        }
        else if (command.Op != "") throw new RuleViolation("exchange.command", "Unknown exchange operation");
        var resources = session.World.Factions.Single(f => f.Id == session.PlayerFaction).Resources;
        return new { session.Revision, commerce, rate, resources, commander = session.World.Units.FirstOrDefault(u => u.Cell == command.Cell)?.CommanderId,
            offers = amounts.Select((amount, index) => new { id = index.ToString(), resource = index < 2 ? "industry" : "food",
                buyAmount = amount, buyMoney = (int)(money[index] * rate), sellAmount = (int)(amount * rate), sellMoney = money[index],
                canBuy = resources.Money >= (int)(money[index] * rate), canSell = (index < 2 ? resources.Industry : resources.Food) >= (int)(amount * rate) }) };
    }

    /// <summary>
    /// 规划并执行 AI 的一步：AI 只产出与玩家完全相同的 Engine 命令，这里逐条改写版本号后经 Apply 执行。
    /// 若中途出现对白或终局，其余命令不再执行。返回含攻击的演出信息（无攻击为 null）。
    /// </summary>
    private object? AiStep()
    {
        var current = Require();
        var rules = PlayEngineRules.ToRuleSet(current.Rules);
        var world = current.Session.World;
        var step = AiPlanner.Next(world, rules, current.Rules.Ai, current.Rules.Production[world.ActiveFaction], aiRandom);
        var movements = new List<object>();
        object? combat = null;
        bool first = true;
        foreach (EngineCommand planned in step.Commands)
        {
            var session = Require().Session;
            if (!first && (flow.Blocked || session.World.Phase == TurnPhase.Finished)) break;
            first = false;
            EngineCommand bound = planned with { ExpectedRevision = session.Revision };
            EngineResult applied = ApplyEngine(bound);
            foreach (EngineEvent happened in applied.Events)
            {
                switch (happened)
                {
                    case UnitMovedEvent moved:
                        movements.Add(new { unit = moved.UnitId, path = moved.Path });
                        break;
                    case AttackResolvedEvent attacked:
                        var preview = attacked.Preview;
                        combat = new { attacker = preview.Attacker, defender = preview.Defender, counterattack = preview.Main.Counterattack,
                        movements = movements.ToArray(), damage = preview.Main.DefenderDamage, counterDamage = preview.Main.AttackerDamage,
                        critical = preview.Main.OffensiveProc && !preview.Main.DefensiveProc, counterCritical = false,
                        splashTarget = preview.SplashTarget, splashDamage = preview.SplashDamage };
                        break;
                }
            }
        }

        aiRandom = step.RandomState;
        return combat;
    }

    /// <summary>HTTP：关卡目录、章节、扩展布局与是否有自动存档。</summary>
    internal object RouteCatalog() => new
    {
        scenarios = content.Scenarios,
        chapters = content.Chapters.Select(c => c with { Countries = c.Countries.Select(content.Countries.Canonical).ToArray() }),
        presentations = content.Presentations.ToDictionary(p => p.Key, p => p.Value with
        {
            Countries = p.Value.Countries.Select(content.Countries.Canonical).ToArray()
        }),
        unlockedCampaigns = Account().UnlockedCampaigns,
        campaignStars = Account().CampaignStars,
        hasSave = PlaySlotPersistence.Exists(Slot),
        countries = content.Countries.Countries,
        countryAliases = content.Countries.Aliases
    };

    /// <summary>HTTP：国家表与别名（schema=1）。</summary>
    internal object RouteCountryRegistry() =>
        new { schema = 1, countries = content.Countries.Countries, aliases = content.Countries.Aliases };

    /// <summary>HTTP：解析 body 后返回 Countries。</summary>
    internal object RouteCountries(string body) => Countries(PlayCommandJson.Parse(body));

    /// <summary>HTTP：解析 body 后返回 LocalPlayers。</summary>
    internal object RouteLocalPlayers(string body) => LocalPlayers(PlayCommandJson.Parse(body));

    /// <summary>HTTP：返回 Headquarters。</summary>
    internal object RouteHeadquarters() => Headquarters();

    /// <summary>HTTP：解析 body 后返回 Deployment。</summary>
    internal object RouteDeployment(string body) => Deployment(PlayCommandJson.Parse(body));

    /// <summary>HTTP：解析 body 后返回 UnitInfo。</summary>
    internal object RouteUnitInfo(string body) => UnitInfo(PlayCommandJson.Parse(body));

    /// <summary>HTTP：解析 body 后执行 CommanderAction。</summary>
    internal object RouteCommander(string body) => CommanderAction(PlayCommandJson.Parse(body));

    /// <summary>HTTP：解析 body 后执行 Commerce。</summary>
    internal object RouteCommerce(string body) => Commerce(PlayCommandJson.Parse(body));

    /// <summary>HTTP：解析 body 后返回 Consumables。</summary>
    internal object RouteItems(string body) => Consumables(PlayCommandJson.Parse(body));

    /// <summary>HTTP：解析 body 后执行 Exchange。</summary>
    internal object RouteExchange(string body) => Exchange(PlayCommandJson.Parse(body));

    /// <summary>HTTP：解析 body 后执行 Start。</summary>
    internal object RouteStart(string body) => Start(PlayCommandJson.Parse(body));

    /// <summary>HTTP：解析 body 后执行 Leave。</summary>
    internal object RouteLeave(string body) => Leave(PlayCommandJson.Parse(body));

    /// <summary>HTTP：解析 body 后执行 Action。</summary>
    internal object RouteAction(string body) => Action(PlayCommandJson.Parse(body));

    /// <summary>HTTP：解析 body 后执行 Save。</summary>
    internal object RouteSave(string body) => Save(PlayCommandJson.ParseOrEmpty(body));

    /// <summary>HTTP：解析 body 后返回 Saves。</summary>
    internal object RouteSaves(string body) => Saves(PlayCommandJson.Parse(body));

    /// <summary>HTTP：解析 body 后执行 Load。</summary>
    internal object RouteLoad(string body) => Load(PlayCommandJson.ParseOrEmpty(body));

    /// <summary>解析槽位号与游戏模式对应的磁盘路径（含旧 autosave 回退）。</summary>
    private string SavePath(int slot, int mode, bool legacyFallback = false)
    {
        SaveSlotId id = new(mode, slot);
        string path;
        try
        {
            path = SaveDirectoryLayout.SlotPathWithLegacyAutosave(saveDirectory, id, legacyFallback);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new RuleViolation("save.slot", "Invalid save slot or game mode");
        }

        if (legacyFallback && mode != 0 && slot == 0
            && path == SaveDirectoryLayout.AutoSavePath(saveDirectory))
        {
            var old = ReadSlot(path);
            if (!content.Scenarios.Any(s => s.Id == old.Scenario && s.Mode == mode))
                path = SaveDirectoryLayout.SlotPath(saveDirectory, id);
        }

        return path;
    }

    /// <summary>读取槽位文件并反序列化为 PlaySlot。</summary>
    private PlaySlot ReadSlot(string path)
    {
        byte[] bytes = PlaySlotPersistence.ReadInnerJson(path, slotEnvelope);

        return JsonSerializer.Deserialize<PlaySlot>(bytes, Json)
            ?? throw new RuleViolation("save.format", "Invalid slot");
    }

    /// <summary>列出 0–6 号槽位的摘要（是否存在、损坏、关卡名与回合）。</summary>
    private object Saves(PlayCommand command) => Enumerable.Range(0, 7).Select(index =>
    {
        SavePath(index, command.Mode);
        try
        {
            string path = SavePath(index, command.Mode, true);
            if (!PlaySlotPersistence.Exists(path)) return new { slot = index, exists = false, corrupt = false, scenario = "", name = "", country = (string?)null, round = 0, savedAt = (DateTimeOffset?)null };
            var saved = ReadSlot(path);
            return new { slot = index, exists = true, corrupt = false, scenario = saved.Scenario,
                name = content.Scenarios.Single(s => s.Id == saved.Scenario).Name, country = saved.Country,
                round = saved.Round, savedAt = (DateTimeOffset?)(saved.SavedAt ?? PlaySlotPersistence.GetLastWriteTime(path)) };
        }
        catch (Exception error) when (error is JsonException or IOException or RuleViolation or InvalidOperationException)
        {
            return new { slot = index, exists = true, corrupt = true, scenario = "", name = "", country = (string?)null, round = 0, savedAt = (DateTimeOffset?)null };
        }
    }).ToArray();

    /// <summary>战役/征服模式在开局或读档后写入模式槽位 0。</summary>
    private void AutoSave()
    {
        var current = Require();
        int mode = content.Scenarios.Single(s => s.Id == current.Session.World.ScenarioId).Mode;
        if (mode is 1 or 2) SaveSlot(new PlayCommand(Slot: 0, Mode: mode, Tutorial: tutorialCheckpoint));
    }

    /// <summary>待处理的场景事件弹窗与选项可用性。</summary>
    private object? ScenarioPopupView(WorldState world)
    {
        var ev = world.ScenarioEvents.FirstOrDefault(e => e.Popup != null && ScenarioRules.DialogueId(e.Id) == PendingFlow);
        return ev == null ? null : new { ev.Id, title = content.Label(ev.Popup!.Title), text = content.Label(ev.Popup.Text),
            image = ev.Popup.Image, imageAlt = content.Label(ev.Popup.ImageAlt),
            choices = ev.Popup.Choices.Select(c => new { c.Id, text = content.Label(c.Text),
                available = c.Condition == null || ScenarioRules.Match(world, c.Condition) }).ToArray() };
    }

    /// <summary>进局/读档时结算战斗奖励、战役进度与 BattleId 迁移。</summary>
    private GameSession SettleBattle(GameSession session)
    {
        if (string.IsNullOrEmpty(session.BattleId))
        {
            // Old saves have no run identity. One conservative migration identity per scenario/player
            // prevents replaying different pre-migration snapshots for repeated conquest rewards.
            string key = session.ContentRevision + ":" + session.World.ScenarioId + ":" + session.PlayerFaction;
            session = session with { BattleId = "legacy-" + Convert.ToHexStringLower(Ew4.Interface.Common.Sha256Hash.HashData(Encoding.UTF8.GetBytes(key))) };
        }
        // Keep progression and one-time receipts without awarding single-player campaign rewards.
        if (session.World.LocalMultiplayer != null)
            return flow.Blocked ? session : BattleSettlement.Settle(session, 4, "", 1, 0);
        var presentation = content.Presentations.Values.FirstOrDefault(p => p.Id == session.World.ScenarioId || p.Alternate == session.World.ScenarioId);
        int mode = content.Scenarios.Single(s => s.Id == session.World.ScenarioId).Mode;
        var source = content.Battle(session.World.ScenarioId);
        session = session with { Account = CampaignProgress.Settle(content, session) };
        return flow.Blocked ? session : BattleSettlement.Settle(session, mode, presentation?.Id ?? session.World.ScenarioId, source.MinimumRound, source.RatingRound);
    }

    /// <summary>战后结算屏：星级、勋章、下一关与参战将领等。</summary>
    private object? BattleResultView(GameSession session)
    {
        var result = session.BattleResult;
        if (result == null) return null;
        string scenario = session.World.ScenarioId;
        var entry = content.Presentations.Values.FirstOrDefault(p => p.Id == scenario || p.Alternate == scenario);
        var next = session.World.LocalMultiplayer != null || entry == null || entry.Chapter == 0 ? null : content.Presentations.Values
            .Where(p => p.Chapter == entry.Chapter && string.CompareOrdinal(p.Id, entry.Id) > 0
                && CampaignProgress.Accessible(content, session.Account, p.Id)
                && (p.Countries.Length == 0 || p.Countries.Select(content.Countries.Canonical).Contains(content.Countries.Canonical(content.Battle(scenario).Factions.Single(f => !f.Ai).CountryId))))
            .OrderBy(p => p.Id, StringComparer.Ordinal).FirstOrDefault();
        var generals = DeployedGeneralsView(session, session.PlayerFaction);
        return new { result.Id, result.Scenario, result.Outcome, result.Round, result.Stars,
            result.MinimumRatingRound, result.HighestRatingRound, result.AwardMedals, result.CollectedMedals,
            name = content.Scenarios.Single(s => s.Id == scenario).Name, nextScenario = next?.Id, generals,
            localMultiplayer = LocalMultiplayerView(session) };
    }

    // Read the current battle instance, including scenario/enemy/tavern commanders.
    // The account roster alone cannot describe these commanders or battle experience.
    /// <summary>部队详情面板：征兵花费与战场将领成长/装备（含非名册将领）。</summary>
    private object UnitInfo(PlayCommand command)
    {
        var current = Require(); var session = current.Session; var world = session.World;
        if (command.Revision != session.Revision) throw new RuleViolation("client.stale", "State changed; refresh the command");
        var unit = world.Units.SingleOrDefault(u => u.Id == command.Unit)
            ?? throw new RuleViolation("client.unit", "Unit is unavailable");
        var offer = current.Rules.Recruitment.Values.SelectMany(v => v.Values)
            .FirstOrDefault(o => o.Template?.Profile.Id == unit.Combatant.Profile.Id);
        object? commander = null;
        if (unit.CommanderId is string id)
        {
            var runtime = world.Commanders.Single(c => c.Id == id);
            var owned = session.Account.Roster.FirstOrDefault(c => c.Id == id);
            var resolved = owned != null ? AccountCommanders.Resolve(owned, current.Rules.Roster.Account)
                : current.Rules.Commanders.Definitions[id];
            var battle = content.Battle(world.ScenarioId);
            int sourceId = owned != null ? content.Catalogs.Runtime.Commanders.Single(c => c.Value.Definition.Id == owned.DefinitionId).Key
                : battle.Armies.Select((a, i) => new { a.Commander, Id = world.ScenarioId + ".unit." + i + ".commander" })
                    .FirstOrDefault(a => a.Id == id)?.Commander
                    ?? battle.Taverns.SelectMany(t => t.Candidates).Select((c, i) => new { c.CommanderId, Id = world.ScenarioId + ".tavern." + i + ".commander" })
                    .Single(c => c.Id == id).CommanderId;
            var source = content.Catalogs.Runtime.Commanders[sourceId];
            var abilities = content.Catalogs.Accounts.FromDefinition(sourceId, "preview").Learning.Abilities.ToDictionary(p => p.Key, p => p.Value);
            if (owned != null) foreach (var value in owned.Learning.Abilities) abilities[value.Key] = value.Value;
            foreach (var value in resolved.Stars) abilities[value.Key] = value.Value;
            abilities["movement"] = resolved.Movement;
            commander = new { id, definitionId = source.Definition.Id, locked = true, battle = true,
                learning = new CommanderLearningState(runtime.Rank, runtime.Title, abilities),
                training = runtime.MaximumTrainingLevel, items = resolved.Items.Select(i => i.Id).ToArray(),
                healthBonus = runtime.RankHealthBonus, healing = current.Rules.Commanders.TitleHealing.GetValueOrDefault(runtime.Title.Level) };
        }
        return new { session.Revision, unit = unit.Id, cost = offer?.Cost, commander };
    }
}
