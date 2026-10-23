using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Ew4.Platform.Content;
using Ew4.Platform.Engine;

namespace Ew4.Platform.Content.Build;

/// <summary>
/// 对齐封存 <c>ClientContent.Start</c> 在 Legacy 开局后的扩展/Victory 规则与剧本事件处理。
/// </summary>
internal static class ScenarioExtensionPostProcessor
{
    internal static BuiltScenario Apply(
        OriginalScenarioBuild built,
        OriginalBattle battle,
        string scenarioId,
        int mode,
        PackRuntimeBootstrap bootstrap)
    {
        ExtensionLoader extensions = bootstrap.Extensions;
        OriginalScenarioCatalogs catalogs = bootstrap.Catalogs;
        string playerFaction = built.PlayerFaction;

        WorldState world = built.InitialWorld with { ScenarioPlayerFaction = playerFaction };
        OriginalScenarioRules rules = built.Rules with
        {
            Battle = built.Rules.Battle with
            {
                Combat = built.Rules.Battle.Combat with { LethalStrikePreventsCounterattack = true },
            },
        };

        if (mode is 1 or 2)
        {
            world = world with
            {
                Jurisdictions = battle.Jurisdictions?.Apply(world) ?? TerritorialJurisdictions.Create(world),
            };
        }

        ExtensionVictoryRules? customVictory = battle.Victory ?? extensions.Victories.GetValueOrDefault(scenarioId);
        if (!extensions.Enabled && customVictory == null)
        {
            return Finish(built, rules, world, scenarioId, mode, bootstrap);
        }

        VictoryRules victory = rules.Turns.Victory with { Custom = customVictory, Scripts = extensions.Bus.Scripts };
        if (customVictory != null)
        {
            ValidateVictoryTargets(customVictory, world);
            extensions.ValidateVictoryScripts(customVictory);
        }

        WorldMoveRules movement = rules.Movement with { Extensions = extensions.Bus, Victory = victory };
        WorldTurnRules turns = rules.Turns with { Extensions = extensions.Bus, Victory = victory };
        WorldBattleRules battleRules = rules.Battle with
        {
            Combat = rules.Battle.Combat with { Extensions = extensions.Bus },
            Victory = victory,
        };
        rules = rules with
        {
            Movement = movement,
            Turns = turns,
            Battle = battleRules,
            Commanders = rules.Commanders with { Extensions = extensions.Bus },
            Roster = rules.Roster with { Assignment = rules.Roster.Assignment with { Extensions = extensions.Bus } },
            Tavern = rules.Tavern with { Assignment = rules.Tavern.Assignment with { Extensions = extensions.Bus } },
            Production = rules.Production.ToDictionary(
                p => p.Key,
                p => p.Value with
                {
                    Commanders = p.Value.Commanders == null
                        ? null
                        : p.Value.Commanders with { Extensions = extensions.Bus },
                },
                StringComparer.Ordinal),
            Ai = rules.Ai with { Movement = movement },
        };

        if (extensions.Enabled)
        {
            string revision = Convert.ToHexStringLower(
                SHA256.HashData(Encoding.UTF8.GetBytes(world.ContentRevision + ":" + extensions.Revision)));
            world = world with
            {
                ContentRevision = revision,
                ScenarioPlayerFaction = playerFaction,
                DiplomacyNeutralCamps = turns.NeutralCamps.ToArray(),
                ScenarioEvents = (extensions.ScenarioEvents.GetValueOrDefault(scenarioId) ?? [])
                    .Select(e => ScenarioFactions.BindTemplates(e, playerFaction))
                    .ToArray(),
            };
            world = world with
            {
                Units = world.Units.Select(unit =>
                {
                    int budget = extensions.Bus.MovementBudget(unit.Combatant);
                    return unit with
                    {
                        ExtensionMovementLimit = budget,
                        RemainingMovement = unit.RemainingMovement == unit.Combatant.Profile.Movement
                            ? budget
                            : Math.Min(unit.RemainingMovement, budget),
                    };
                }).ToArray(),
            };
            ScenarioRules.Validate(world, initial: true);

            var production = rules.Production.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            var recruitment = rules.Recruitment.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            var grants = new Dictionary<string, IReadOnlyDictionary<string, RecruitmentGrant>>();
            foreach (ScenarioEffect creation in ScenarioFactions.Creations(world.ScenarioEvents))
            {
                string? template = ScenarioFactions.Template(world, creation);
                WorldProductionRules policy;
                if (creation.UnitVariant == null && template != null)
                {
                    policy = production[template];
                }
                else
                {
                    string variant = creation.UnitVariant ?? "others";
                    if (!catalogs.Runtime.Variants.ContainsKey(variant))
                    {
                        throw new RuleViolation("scenario.unit_variant", "Unknown unit variant: " + variant);
                    }

                    policy = OriginalAiCatalog.Production(variant, catalogs.Runtime, catalogs.Cards, catalogs.Buildings)
                        with { Commanders = rules.Commanders };
                }

                production.Add(creation.Faction, policy);
                Dictionary<string, RecruitmentOffer> offers = policy.Offers.Concat(policy.Fortresses)
                    .ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
                recruitment.Add(creation.Faction, offers);
                IReadOnlyDictionary<string, RecruitmentGrant> available = creation.Recruitment
                    ?? (creation.UnitVariant == null && template != null
                        ? world.Factions.Single(f => f.Id == template).Recruitment
                        : offers.ToDictionary(p => p.Key, _ => new RecruitmentGrant()));
                if (available.Keys.Any(key => !offers.ContainsKey(key)))
                {
                    throw new RuleViolation("scenario.recruitment", "Unknown recruitment card for " + creation.Faction);
                }

                grants.Add(creation.Faction, available);
            }

            ScenarioEffect Prepare(ScenarioEffect effect) =>
                effect.Type == "faction_create"
                    ? effect with { Recruitment = grants[effect.Faction] }
                    : effect;

            world = world with
            {
                ScenarioEvents = world.ScenarioEvents.Select(ev => ev with
                {
                    Effects = ev.Effects.Select(Prepare).ToArray(),
                    InsufficientResourcesEffects = ev.InsufficientResourcesEffects.Select(Prepare).ToArray(),
                    Popup = ev.Popup == null
                        ? null
                        : ev.Popup with
                        {
                            Choices = ev.Popup.Choices.Select(c => c with { Effects = c.Effects.Select(Prepare).ToArray() }).ToArray(),
                        },
                }).ToArray(),
            };
            rules = rules with { Production = production, Recruitment = recruitment };

            foreach (string unit in world.Units.Select(u => u.Id).ToArray())
            {
                world = extensions.Bus.Dispatch(world, "scenario.start", unit, new Ew4Random(world.RandomState));
            }

            world.Validate();
        }

        WorldChange scenarioChange = ScenarioRules.Evaluate(world, extensions.Bus.Scripts);
        world = scenarioChange.State;

        if ((customVictory != null || world.ScenarioVictory != null)
            && world.PendingDialogue == null
            && world.Outcome == BattleOutcome.Continue)
        {
            BattleOutcome outcome = WorldVictory.Evaluate(
                world,
                turns.PlayerCamp!,
                victory,
                turns.NeutralCamps);
            if (outcome != BattleOutcome.Continue)
            {
                world = world with { Outcome = outcome, Phase = TurnPhase.Finished };
            }
        }

        return Finish(built, rules, world, scenarioId, mode, bootstrap);
    }

    private static BuiltScenario Finish(
        OriginalScenarioBuild built,
        OriginalScenarioRules rules,
        WorldState world,
        string scenarioId,
        int mode,
        PackRuntimeBootstrap bootstrap)
    {
        BuiltSessionAttach session = ScenarioSessionBootstrap.Create(
            built,
            rules,
            scenarioId,
            bootstrap.Catalogs.Accounts.SourceRevision,
            bootstrap.Extensions,
            world.ContentRevision);
        return new BuiltScenario(world, RuleSetMapping.FromLegacy(rules, mode, built.PlayerFaction), session);
    }

    private static void ValidateVictoryTargets(ExtensionVictoryRules rules, WorldState world)
    {
        void Check(VictoryCondition c)
        {
            if (c.Cells.Any(i => i < 0 || i >= world.Cells.Count || !world.Cells[i].Terrain.Active)
                || c.Factions.Any(id => world.Factions.All(f => f.Id != id))
                || c.Units.Any(id => world.Units.All(u => u.Id != id)))
            {
                throw new RuleViolation(
                    "extension.victory_reference",
                    "Victory condition references an unknown initial unit/faction/cell");
            }

            foreach (VictoryCondition child in c.Conditions)
            {
                Check(child);
            }
        }

        Check(rules.Win);
        if (rules.Lose != null)
        {
            Check(rules.Lose);
        }
    }
}
