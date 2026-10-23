using Ew4.Platform.Content.Build;
using Ew4.Platform.Engine;

namespace Ew4.Service;

public sealed partial class ClientContent
{
    public OriginalScenarioStart Start(string id, string? country, PlayerAccount? account = null, IReadOnlyList<string>? localPlayers = null)
    {
        var choice = Scenarios.Single(s => s.Id == id);
        if (choice.Mode == 3) throw new RuleViolation("client.mode", "Multiplayer requires a multiplayer session host");
        account ??= new PlayerAccount { Id = "local", ContentRevision = Catalogs.Accounts.SourceRevision, Medals = 20_000_000 };
        var battle = Battle(id);
        // The UI uses canonical registry IDs while legacy conquest records still
        // carry their fixed-width source IDs.
        string? selectedCountry = country == null ? null
            : battle.Factions.FirstOrDefault(f => Countries.Canonical(f.CountryId) == country)?.CountryId ?? country;
        if (battle.Jurisdictions != null && choice.Mode is not (1 or 2))
            throw new RuleViolation("client.jurisdiction_mode", "手工辖区仅支持战役与征服剧本");
        var started = OriginalScenarioHost.Start(battle, Maps, Catalogs, new OriginalScenarioRequest
        {
            Id = id, Mode = choice.Mode, SelectedCountry = selectedCountry, LocalPlayers = localPlayers,
            CityNames = scenarioCityNames.TryGetValue(id, out var names) ? names : Cities,
            Warzone = Presentations.Values.FirstOrDefault(p => p.Id == id || p.Alternate == id)?.Chapter is int chapter && chapter > 0 ? chapter - 1 : 0,
            CountryNames = battle.Factions.Select(f => f.CountryId).Distinct().ToDictionary(c => c, CountryName),
            CountryUnitVariants = battle.Factions.Where(f => Country(f.CountryId)?.UnitVariant is string variant && variant != f.Variant
                && !(variant == "others" && !Catalogs.Runtime.Variants.ContainsKey(f.Variant)))
                .GroupBy(f => f.CountryId).ToDictionary(g => g.Key, g => Country(g.Key)!.UnitVariant!),
            CombatSeed = choice.Mode == 4 ? 100 : (ulong)Random.Shared.NextInt64(1, long.MaxValue), AiSeed = (ulong)Random.Shared.NextInt64(1, long.MaxValue)
        }, account);
        if (Extensions.DeploymentLimits.TryGetValue(id, out int? limit) || Extensions.DeploymentLimits.TryGetValue("*", out limit))
            started = started with { Session = started.Session with {
                RosterDeployments = started.Session.RosterDeployments.Select(q => q with { Limit = limit }).ToArray() } };
        // Apply playable rules after import so existing scenario/save fingerprints stay compatible.
        started = started with { Rules = started.Rules with { Battle = started.Rules.Battle with {
            Combat = started.Rules.Battle.Combat with { LethalStrikePreventsCounterattack = true } } } };
        if (choice.Mode is 1 or 2)
        {
            var world = started.Session.World;
            started = started with { Session = started.Session with { World = world with {
                Jurisdictions = battle.Jurisdictions?.Apply(world) ?? TerritorialJurisdictions.Create(world) } } };
        }
        var customVictory = battle.Victory ?? Extensions.Victories.GetValueOrDefault(id);
        if (Extensions.Enabled || customVictory != null)
        {
            var rules = started.Rules;
            var victory = rules.Turns.Victory with { Custom = customVictory, Scripts = Extensions.Bus.Scripts };
            if (customVictory != null)
            {
                ValidateVictoryTargets(customVictory, started.Session.World);
                Extensions.ValidateVictoryScripts(customVictory);
            }
            var movement = rules.Movement with { Extensions = Extensions.Bus, Victory = victory };
            var turns = rules.Turns with { Extensions = Extensions.Bus, Victory = victory };
            var combat = rules.Battle with { Combat = rules.Battle.Combat with { Extensions = Extensions.Bus }, Victory = victory };
            started = started with { Rules = rules with { Movement = movement, Turns = turns, Battle = combat,
                Commanders = rules.Commanders with { Extensions = Extensions.Bus },
                Roster = rules.Roster with { Assignment = rules.Roster.Assignment with { Extensions = Extensions.Bus } },
                Tavern = rules.Tavern with { Assignment = rules.Tavern.Assignment with { Extensions = Extensions.Bus } },
                Production = rules.Production.ToDictionary(p => p.Key, p => p.Value with {
                    Commanders = p.Value.Commanders == null ? null : p.Value.Commanders with { Extensions = Extensions.Bus } }),
                Ai = rules.Ai with { Movement = movement } } };
            if (Extensions.Enabled)
            {
                var world = started.Session.World;
                string revision = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(world.ContentRevision + ":" + Extensions.Revision)));
                world = world with { ContentRevision = revision, ScenarioPlayerFaction = started.Session.PlayerFaction, DiplomacyNeutralCamps = turns.NeutralCamps.ToArray(),
                    ScenarioEvents = (Extensions.ScenarioEvents.GetValueOrDefault(id) ?? [])
                        .Select(e => ScenarioFactions.BindTemplates(e, started.Session.PlayerFaction)).ToArray() };
                world = world with { Units = world.Units.Select(unit => {
                    int budget = Extensions.Bus.MovementBudget(unit.Combatant);
                    return unit with { ExtensionMovementLimit = budget, RemainingMovement = unit.RemainingMovement == unit.Combatant.Profile.Movement
                        ? budget : Math.Min(unit.RemainingMovement, budget) };
                }).ToArray() };
                ScenarioRules.Validate(world, initial: true);
                var production = started.Rules.Production.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
                var recruitment = started.Rules.Recruitment.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
                var grants = new Dictionary<string, IReadOnlyDictionary<string, RecruitmentGrant>>();
                foreach (var creation in ScenarioFactions.Creations(world.ScenarioEvents))
                {
                    string? template = ScenarioFactions.Template(world, creation);
                    WorldProductionRules policy;
                    if (creation.UnitVariant == null && template != null) policy = production[template];
                    else
                    {
                        string variant = creation.UnitVariant ?? "others";
                        if (!Catalogs.Runtime.Variants.ContainsKey(variant)) throw new RuleViolation("scenario.unit_variant", "Unknown unit variant: " + variant);
                        policy = OriginalAiCatalog.Production(variant, Catalogs.Runtime, Catalogs.Cards, Catalogs.Buildings) with { Commanders = started.Rules.Commanders };
                    }
                    production.Add(creation.Faction, policy);
                    var offers = policy.Offers.Concat(policy.Fortresses).ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
                    recruitment.Add(creation.Faction, offers);
                    var available = creation.Recruitment ?? (creation.UnitVariant == null && template != null
                        ? world.Factions.Single(f => f.Id == template).Recruitment : offers.ToDictionary(p => p.Key, _ => new RecruitmentGrant()));
                    if (available.Keys.Any(key => !offers.ContainsKey(key))) throw new RuleViolation("scenario.recruitment", "Unknown recruitment card for " + creation.Faction);
                    grants.Add(creation.Faction, available);
                }
                ScenarioEffect Prepare(ScenarioEffect effect) => effect.Type == "faction_create" ? effect with { Recruitment = grants[effect.Faction] } : effect;
                world = world with { ScenarioEvents = world.ScenarioEvents.Select(ev => ev with {
                    Effects = ev.Effects.Select(Prepare).ToArray(), InsufficientResourcesEffects = ev.InsufficientResourcesEffects.Select(Prepare).ToArray(),
                    Popup = ev.Popup == null ? null : ev.Popup with { Choices = ev.Popup.Choices.Select(c => c with { Effects = c.Effects.Select(Prepare).ToArray() }).ToArray() }
                }).ToArray() };
                started = started with { Rules = started.Rules with { Production = production, Recruitment = recruitment } };
                foreach (string unit in world.Units.Select(u => u.Id).ToArray())
                    world = Extensions.Bus.Dispatch(world, "scenario.start", unit, new Ew4Random(world.RandomState));
                world.Validate();
                started = started with { Session = started.Session with { World = world } };
            }
            var scenarioChange = ScenarioRules.Evaluate(started.Session.World, Extensions.Bus.Scripts);
            started = started with { Session = SessionActions.SyncPlayer(started.Session with { World = scenarioChange.State }),
                Notices = started.Notices.Concat(scenarioChange.Notices).ToArray() };
            if ((customVictory != null || started.Session.World.ScenarioVictory != null) && started.Session.World.PendingDialogue == null && started.Session.World.Outcome == BattleOutcome.Continue)
            {
                var world = started.Session.World;
                var outcome = WorldVictory.Evaluate(world, turns.PlayerCamp!, victory, turns.NeutralCamps);
                if (outcome != BattleOutcome.Continue)
                    started = started with { Session = started.Session with { World = world with { Outcome = outcome, Phase = TurnPhase.Finished } } };
            }
        }
        if (localPlayers != null)
            started = started with { Session = SessionActions.SyncPlayer(started.Session with {
                World = LocalMultiplayerGame.Resolve(started.Session.World, started.Rules.Turns.Victory) }) };
        return started;
    }

    private static void ValidateVictoryTargets(ExtensionVictoryRules rules, WorldState world)
    {
        void Check(VictoryCondition c)
        {
            if (c.Cells.Any(i => i < 0 || i >= world.Cells.Count || !world.Cells[i].Terrain.Active)
                || c.Factions.Any(id => world.Factions.All(f => f.Id != id)) || c.Units.Any(id => world.Units.All(u => u.Id != id)))
                throw new RuleViolation("extension.victory_reference", "Victory condition references an unknown initial unit/faction/cell");
            foreach (var child in c.Conditions) Check(child);
        }
        Check(rules.Win); if (rules.Lose != null) Check(rules.Lose);
    }
}
