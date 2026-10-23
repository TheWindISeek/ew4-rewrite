using System;
using System.Linq;
using Ew4.Platform.Content.Build;
using Ew4.Platform.Engine;

namespace Ew4.Platform.Content;

public static class ScenarioStartHelpers
{
    public static int ResolveMode(string scenarioId)
    {
        if (scenarioId.StartsWith("conquest", StringComparison.Ordinal))
        {
            return 2;
        }

        if (scenarioId.StartsWith("tutorial", StringComparison.Ordinal))
        {
            return 4;
        }

        if (scenarioId.StartsWith("multiplay", StringComparison.Ordinal))
        {
            return 3;
        }

        return 1;
    }

    public static bool SupportsSmokeStart(int mode) => mode is 1 or 2 or 4;

    public static string? SelectConquestCountry(OriginalBattle battle)
    {
        OriginalFaction? human = battle.Factions.FirstOrDefault(f => !f.Ai);
        if (human != null)
        {
            return human.CountryId;
        }

        return battle.Factions.Count > 0 ? battle.Factions[0].CountryId : null;
    }

    public static int ResolveCampaignWarzone(string scenarioId)
    {
        if (!scenarioId.StartsWith("campaign", StringComparison.Ordinal))
        {
            return 0;
        }

        int underscore = scenarioId.IndexOf('_', StringComparison.Ordinal);
        if (underscore <= 8)
        {
            return 0;
        }

        return int.TryParse(scenarioId.AsSpan(8, underscore - 8), out int chapter) && chapter > 0 ? chapter - 1 : 0;
    }

    public static PlayerAccount SmokeAccount(string contentRevision) =>
        new()
        {
            Id = "local",
            ContentRevision = contentRevision,
            Medals = 20_000_000,
        };
}
