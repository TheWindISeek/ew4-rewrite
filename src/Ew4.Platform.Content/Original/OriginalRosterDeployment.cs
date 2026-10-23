using Ew4.Platform.Engine;

namespace Ew4.Platform.Content.Original;

public static class OriginalRosterDeployment
{
    public static RosterDeploymentState Start(OriginalFaction faction, string factionId, bool preserveNativeLimit = false)
    {
        if (string.IsNullOrWhiteSpace(factionId) || faction.Count172 < 0)
            throw new RuleViolation("legacy.roster_quota", "Invalid scenario deployment quota");
        return new(factionId, preserveNativeLimit ? Math.Min(12, faction.Count172) : faction.Count172, Array.Empty<string>());
    }

    public static RosterDeploymentState Restore(ReadOnlySpan<byte> savedFaction, string factionId, int? limit,
        IReadOnlyDictionary<int, string> ownedIdentities)
    {
        if (savedFaction.Length != 428 || string.IsNullOrWhiteSpace(factionId) || limit < 0)
            throw new RuleViolation("legacy.roster_history", "Invalid saved faction or deployment policy");
        var ids = new List<string>(); bool ended = false;
        for (int i = 0; i < 12; i++)
        {
            int id = OriginalBytes.I16(savedFaction, 0xd4 + i * 2);
            if (id == -1) { ended = true; continue; }
            if (ended || id < 0 || !ownedIdentities.TryGetValue(id, out string? identity) || string.IsNullOrWhiteSpace(identity)
                || ids.Contains(identity, StringComparer.Ordinal))
                throw new RuleViolation("legacy.roster_history", "Unresolved, duplicate or noncontiguous saved deployment history");
            ids.Add(identity);
        }
        if (ids.Count > limit) throw new RuleViolation("legacy.roster_history", "Saved deployments exceed the configured quota");
        return new(factionId, limit, ids.AsReadOnly());
    }
}
