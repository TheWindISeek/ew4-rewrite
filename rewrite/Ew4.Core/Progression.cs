using System;
using System.Collections.Generic;
using System.Linq;

namespace Ew4.Core;

public sealed record ProgressionLevel(string Id, long? ExperienceToNext, int HealingOnEntry = 0);
public sealed record ProgressionState(int Level, long Experience);
public sealed record ProgressionRules
{
    public IReadOnlyList<ProgressionLevel> Levels { get; init; } = Array.Empty<ProgressionLevel>();
    public bool SingleAdvancePerAward { get; init; }
    public bool DiscardExperienceOnReachingCap { get; init; } = true;
    public bool AccumulateAtCap { get; init; }
}
public sealed record ProgressionResult(ProgressionState State, int LevelsGained, long Healing);

public static class Progression
{
    public static ProgressionResult Award(ProgressionState state, long amount, ProgressionRules rules)
    {
        if (rules.Levels.Count == 0 || rules.Levels.Any(l => string.IsNullOrWhiteSpace(l.Id) || l.HealingOnEntry < 0)
            || rules.Levels.Select(l => l.Id).Distinct(StringComparer.Ordinal).Count() != rules.Levels.Count
            || rules.Levels.Take(rules.Levels.Count - 1).Any(l => !l.ExperienceToNext.HasValue || l.ExperienceToNext <= 0)
            || rules.Levels[rules.Levels.Count - 1].ExperienceToNext != null)
            throw new RuleViolation("progression.configuration", "Invalid progression stages");
        if (state.Level < 0 || state.Level >= rules.Levels.Count || state.Experience < 0 || amount < 0)
            throw new RuleViolation("progression.state", "Invalid experience or current stage");
        if (state.Level == rules.Levels.Count - 1 && !rules.AccumulateAtCap)
            return new ProgressionResult(state, 0, 0);
        int level = state.Level;
        long xp = checked(state.Experience + amount), healing = 0;
        while (rules.Levels[level].ExperienceToNext is long cost && xp >= cost)
        {
            xp -= cost;
            level++;
            healing = checked(healing + rules.Levels[level].HealingOnEntry);
            if (level == rules.Levels.Count - 1 && rules.DiscardExperienceOnReachingCap) xp = 0;
            if (rules.SingleAdvancePerAward) break;
        }
        return new ProgressionResult(new ProgressionState(level, xp), level - state.Level, healing);
    }
}

public static class Ew4Experience
{
    public static long CommanderGain(long amount, bool nobility, IReadOnlyCollection<string> skills,
        int? equipmentPercent = null, bool training = false)
    {
        if (amount < 0 || equipmentPercent < 0) throw new RuleViolation("experience.gain", "Invalid XP gain modifier");
        if (training) return amount;
        if (equipmentPercent.HasValue) return checked(amount * equipmentPercent.Value) / 100;
        float factor = nobility ? (skills.Contains(Ew4Combat.Skill(19)) ? 1.5f : 1f)
            : skills.Contains(Ew4Combat.Skill(21)) ? 1.8f : skills.Contains(Ew4Combat.Skill(20)) ? 1.4f : 1f;
        return factor == 1f ? amount : checked((long)((float)amount * factor));
    }
}
