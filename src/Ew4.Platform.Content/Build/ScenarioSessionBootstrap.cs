using System;
using System.Collections.Generic;
using System.Linq;
using Ew4.Platform.Content;
using Ew4.Platform.Engine;

namespace Ew4.Platform.Content.Build;

internal static class ScenarioSessionBootstrap
{
    internal static BuiltSessionAttach Create(
        OriginalScenarioBuild built,
        OriginalScenarioRules rules,
        string scenarioId,
        string accountContentRevision,
        ExtensionLoader extensions,
        string worldContentRevision)
    {
        int? deploymentLimit = null;
        if (extensions.DeploymentLimits.TryGetValue(scenarioId, out int? limit))
        {
            deploymentLimit = limit;
        }
        else if (extensions.DeploymentLimits.TryGetValue("*", out limit))
        {
            deploymentLimit = limit;
        }

        int rosterLimit = deploymentLimit ?? built.PlayerRosterQuota;
        var roster = new[]
        {
            new RosterDeploymentState(built.PlayerFaction, rosterLimit, Array.Empty<string>()),
        };

        string sessionRevision = OriginalScenarioHost.FingerprintSession(
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
