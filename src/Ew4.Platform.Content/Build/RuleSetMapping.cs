using System;
using System.Collections.Generic;
using System.Linq;
using Ew4.Platform.Engine;

namespace Ew4.Platform.Content.Build;

internal static class RuleSetMapping
{
    internal static RuleSet FromLegacy(OriginalScenarioRules rules, int mode, string playerFaction) =>
        new(
            rules.Turns,
            rules.Movement,
            rules.Battle,
            rules.Ai,
            rules.Production,
            rules.Recruitment,
            rules.Commanders,
            rules.Roster,
            rules.Training,
            rules.Upgrades,
            rules.Installations,
            rules.Transport,
            rules.Tavern,
            rules.Trading,
            BuildDisband(rules, mode, playerFaction),
            Array.Empty<string>());

    private static DisbandRules BuildDisband(OriginalScenarioRules rules, int mode, string playerFaction)
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
