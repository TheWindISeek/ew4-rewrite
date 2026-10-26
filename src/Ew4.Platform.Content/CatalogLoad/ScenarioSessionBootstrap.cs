using System;
using System.Linq;
using Ew4.Platform.Content;
using Ew4.Platform.Engine;

namespace Ew4.Platform.Content.CatalogLoad;

internal static class ScenarioSessionBootstrap
{
    internal static BuiltSessionAttach Create(
        StaticDataScenarioBuild built,
        StaticDataScenarioRules rules,
        string scenarioId,
        string accountContentRevision,
        string worldContentRevision)
    {
        int rosterLimit = built.PlayerRosterQuota;
        var roster = new[]
        {
            new RosterDeploymentState(built.PlayerFaction, rosterLimit, Array.Empty<string>()),
        };

        string sessionRevision = StaticDataScenarioHost.FingerprintSession(
            worldContentRevision,
            accountContentRevision,
            rules.Roster,
            built.Commerce.SourceRevision);

        return new BuiltSessionAttach(
            roster,
            built.Commerce.Markets,
            built.Commerce.Candidates,
            sessionRevision);
    }
}
