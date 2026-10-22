using System.Security.Cryptography;
using System.Text;
using Ew4.Core;

namespace Ew4.Client;

public sealed partial class ClientGame
{
    private GameSession SettleBattle(GameSession session)
    {
        if (string.IsNullOrEmpty(session.BattleId))
        {
            // Old saves have no run identity. One conservative migration identity per scenario/player
            // prevents replaying different pre-migration snapshots for repeated conquest rewards.
            string key = session.ContentRevision + ":" + session.World.ScenarioId + ":" + session.PlayerFaction;
            session = session with { BattleId = "legacy-" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(key))) };
        }
        // Keep progression and one-time receipts without awarding single-player campaign rewards.
        if (session.World.LocalMultiplayer != null)
            return BattleSettlement.Settle(session, 4, "", 1, 0);
        var presentation = content.Presentations.Values.FirstOrDefault(p => p.Id == session.World.ScenarioId || p.Alternate == session.World.ScenarioId);
        int mode = content.Scenarios.Single(s => s.Id == session.World.ScenarioId).Mode;
        var source = content.Battle(session.World.ScenarioId);
        session = session with { Account = CampaignProgress.Settle(content, session) };
        return BattleSettlement.Settle(session, mode, presentation?.Id ?? session.World.ScenarioId, source.Header[12], source.Header[13]);
    }

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
        var ids = session.RosterDeployments.Where(q => q.Faction == session.PlayerFaction).SelectMany(q => q.CommanderIds).ToHashSet();
        var generals = session.Account.Roster.Where(c => ids.Contains(c.Id)).Select(c => {
            var definition = content.Catalogs.Accounts.Rules().Definitions[c.DefinitionId];
            var runtime = session.World.Commanders.FirstOrDefault(g => g.Id == c.Id);
            return new { c.Id, name = content.Label(definition.DisplayName), image = definition.DisplayName + ".png",
                rank = runtime?.Rank.Level ?? c.Learning.Rank.Level, title = runtime?.Title.Level ?? c.Learning.Title.Level,
                withdrawn = runtime?.Withdrawn ?? false };
        }).ToArray();
        return new { result.Id, result.Scenario, result.Outcome, result.Round, result.Stars,
            result.MinimumRatingRound, result.HighestRatingRound, result.AwardMedals, result.CollectedMedals,
            name = content.Scenarios.Single(s => s.Id == scenario).Name, nextScenario = next?.Id, generals,
            localMultiplayer = LocalMultiplayerView(session),
            presentation = content.Extensions.VictoryPresentations.TryGetValue(scenario, out var screen) ? screen
                : content.Extensions.VictoryPresentations.GetValueOrDefault("*") };
    }
}
