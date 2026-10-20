using Ew4.Core;

namespace Ew4.Client;

public sealed partial class ClientGame
{
    private DisbandRules DisbandConfiguration()
    {
        var current = Require();
        bool conquest = content.Scenarios.Single(s => s.Id == current.Session.World.ScenarioId).Mode == 2;
        var offers = current.Rules.Recruitment[current.Session.PlayerFaction];
        return new() { Enabled = conquest, PlayerFaction = current.Session.PlayerFaction,
            RecruitmentCosts = offers.Values.Where(o => o.Template != null).GroupBy(o => o.Template!.Profile.Id)
                .ToDictionary(g => g.Key, g => g.First().Cost, StringComparer.Ordinal) };
    }
}
