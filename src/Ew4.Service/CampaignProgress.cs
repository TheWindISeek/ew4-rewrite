using Ew4.Platform.Engine;

namespace Ew4.Service;

public static class CampaignProgress
{
    public static bool Accessible(ClientContent content, PlayerAccount account, string id)
    {
        var entry = content.Presentations.Values.FirstOrDefault(p => p.Id == id || p.Alternate == id);
        return entry == null || !entry.Hidden || account.UnlockedCampaigns.Contains(entry.Id);
    }

    public static int RemainingYellow(ClientContent content, WorldState world)
    {
        var source = content.Battle(world.ScenarioId);
        var ai = world.Factions.Where(f => !f.Human).Select(f => f.Id).ToHashSet(StringComparer.Ordinal);
        var markedUnits = source.Armies.Select((u, i) => (u, id: world.ScenarioId + ".unit." + i))
            .Where(p => p.u.Objective == 2).Select(p => p.id).ToHashSet(StringComparer.Ordinal);
        // Original 58eb4 counts site and army markers separately, across every AI country.
        return source.EffectiveSites.Count(s => s.Marker == 2 && s.Cell >= 0 && s.Cell < world.Cells.Count
                && world.Cells[s.Cell].Terrain.Active && world.Cells[s.Cell].Owner is string owner && ai.Contains(owner))
            + world.Units.Count(u => markedUnits.Contains(u.Id) && ai.Contains(u.Owner));
    }

    public static PlayerAccount Settle(ClientContent content, GameSession session)
    {
        var world = session.World;
        if (world.Outcome != BattleOutcome.Victory) return session.Account;
        var current = content.Presentations.Values.FirstOrDefault(p => p.Chapter > 0 && (p.Id == world.ScenarioId || p.Alternate == world.ScenarioId));
        if (current == null || RemainingYellow(content, world) != 0) return session.Account;
        int stage = int.Parse(current.Id[(current.Id.IndexOf('_') + 1)..]);
        string playerCountry = content.Countries.Canonical(content.Battle(world.ScenarioId).Factions.Single(f => !f.Ai).CountryId);
        var candidates = Enumerable.Range(stage + 1, current.Countries.Length == 0 ? 1 : 2)
            .Select(n => content.Presentations.GetValueOrDefault($"campaign{current.Chapter}_{n:00}"));
        var next = candidates.FirstOrDefault(p => p != null && (current.Countries.Length == 0 || p.Countries.Select(content.Countries.Canonical).SequenceEqual(new[] { playerCountry })));
        if (next == null || !next.Hidden || session.Account.UnlockedCampaigns.Contains(next.Id)) return session.Account;
        return session.Account.Copy() with { Revision = checked(session.Account.Revision + 1),
            UnlockedCampaigns = session.Account.UnlockedCampaigns.Append(next.Id).ToArray() };
    }
}
