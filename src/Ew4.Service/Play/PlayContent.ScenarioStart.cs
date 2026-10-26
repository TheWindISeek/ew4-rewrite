using Ew4.Platform.Content;
using Ew4.Platform.Content.CatalogLoad;
using Ew4.Platform.Engine;

namespace Ew4.Service.Play;

public sealed partial class PlayContent
{
    public StaticDataScenarioStart Start(string id, string? country, PlayerAccount? account = null, IReadOnlyList<string>? localPlayers = null)
    {
        var choice = Scenarios.Single(s => s.Id == id);
        if (choice.Mode == 3) throw new RuleViolation("client.mode", "Multiplayer requires a multiplayer session host");
        account ??= new PlayerAccount { Id = "local", ContentRevision = Catalogs.Accounts.SourceRevision, Medals = 20_000_000 };
        var battle = Battle(id);
        string? selectedCountry = country == null ? null
            : battle.Factions.FirstOrDefault(f => Countries.Canonical(f.CountryId) == country)?.CountryId ?? country;
        if (battle.Jurisdictions != null && choice.Mode is not (1 or 2))
            throw new RuleViolation("client.jurisdiction_mode", "手工辖区仅支持战役与征服剧本");
        var started = StaticDataScenarioHost.Start(battle, Maps, Catalogs, new StaticDataScenarioRequest
        {
            Id = id, Mode = choice.Mode, SelectedCountry = selectedCountry, LocalPlayers = localPlayers,
            CityNames = scenarioCityNames.TryGetValue(id, out var names) ? names : Cities,
            Warzone = Presentations.Values.FirstOrDefault(p => p.Id == id || p.Alternate == id)?.Chapter is int chapter && chapter > 0 ? chapter - 1 : 0,
            CountryNames = battle.Factions.Select(f => f.CountryId).Distinct().ToDictionary(c => c, CountryName),
            CountryUnitVariants = battle.Factions.Where(f => Country(f.CountryId)?.UnitVariant is string variant && variant != f.Variant
                && !(variant == "others" && !Catalogs.Runtime.Variants.ContainsKey(f.Variant)))
                .GroupBy(f => f.CountryId).ToDictionary(g => g.Key, g => Country(g.Key)!.UnitVariant!),
            CombatSeed = choice.Mode == 4 ? 100 : (ulong)Random.Shared.NextInt64(1, long.MaxValue)
        }, account);
        started = started with { Rules = started.Rules with { Battle = started.Rules.Battle with {
            Combat = started.Rules.Battle.Combat with { LethalStrikePreventsCounterattack = true } } } };
        if (choice.Mode is 1 or 2)
        {
            var world = started.Session.World;
            started = started with { Session = started.Session with { World = world with {
                Jurisdictions = battle.Jurisdictions?.Apply(world) ?? TerritorialJurisdictionBuilder.Create(world) } } };
        }

        var customVictory = battle.Victory;
        if (customVictory != null)
        {
            var rules = started.Rules;
            ExtensionVictory.ValidateTargets(customVictory, started.Session.World, initial: true);
            var victory = rules.Turns.Victory with { Custom = customVictory };
            var movement = rules.Movement with { Victory = victory };
            var turns = rules.Turns with { Victory = victory };
            var combat = rules.Battle with { Victory = victory };
            started = started with { Rules = rules with { Movement = movement, Turns = turns, Battle = combat } };
            var scenarioChange = ScenarioRules.Evaluate(started.Session.World);
            started = started with { Session = SessionActions.SyncPlayer(started.Session with { World = scenarioChange.State }),
                Notices = started.Notices.Concat(scenarioChange.Notices).ToArray() };
            if (started.Session.World.PendingDialogue == null && started.Session.World.Outcome == BattleOutcome.Continue)
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
}
