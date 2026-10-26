using System;
using System.Collections.Generic;
using System.Linq;
using Ew4.Platform.Engine;

namespace Ew4.Platform.Content.CatalogLoad;

internal static class RuleSetMapping
{
    internal static RuleSet FromLegacy(StaticDataScenarioRules rules, int mode, string playerFaction)
    {
        RuleSet result = Build(rules, mode, playerFaction);
        result.Validate();
        rules.Roster.Validate();
        rules.Tavern.Assignment.Validate();
        rules.Trading.Items.Validate();
        return result;
    }

    private static RuleSet Build(StaticDataScenarioRules rules, int mode, string playerFaction) =>
        new(
            rules.Turns,
            rules.Movement,
            rules.Battle,
            rules.Recruitment,
            rules.Commanders,
            rules.Training,
            rules.Upgrades,
            rules.Installations,
            rules.Transport,
            BuildDisband(rules, mode, playerFaction),
            Array.Empty<string>());

    private static DisbandRules BuildDisband(StaticDataScenarioRules rules, int mode, string playerFaction)
    {
        if (mode != 2 || !rules.Recruitment.TryGetValue(playerFaction, out var offers))
        {
            return new DisbandRules { Enabled = false, PlayerFaction = playerFaction };
        }

        return new DisbandRules
        {
            Enabled = true,
            PlayerFaction = playerFaction,
            RecruitmentCosts = offers.Values.Where(o => o.Template != null)
                .GroupBy(o => o.Template!.Profile.Id)
                .ToDictionary(g => g.Key, g => g.First().Cost, StringComparer.Ordinal),
        };
    }
}
