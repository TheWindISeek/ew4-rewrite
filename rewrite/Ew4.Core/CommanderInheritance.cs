using System;
using System.Collections.Generic;
using System.Linq;

namespace Ew4.Core;

public sealed record CommanderLearningState(ProgressionState Rank, ProgressionState Title, IReadOnlyDictionary<string, int> Abilities);
public sealed record InheritedAbilityRule(string Skill, string Ability, int Gain = 1, int? Maximum = null);
public sealed record CommanderInheritanceRules
{
    public ProgressionRules Rank { get; init; } = new();
    public ProgressionRules Title { get; init; } = new();
    public long RankBase { get; init; } = 300;
    public long TitleBase { get; init; } = 60;
    public IReadOnlyList<int> RankPercent { get; init; } = Array.Empty<int>();
    public IReadOnlyList<int> TitlePercent { get; init; } = Array.Empty<int>();
    public IReadOnlyList<InheritedAbilityRule> Abilities { get; init; } = Array.Empty<InheritedAbilityRule>();

    public void Validate()
    {
        Progression.Award(new(0, 0), 0, Rank); Progression.Award(new(0, 0), 0, Title);
        if (RankBase < 0 || TitleBase < 0 || RankPercent.Count != Rank.Levels.Count || TitlePercent.Count != Title.Levels.Count
            || RankPercent.Concat(TitlePercent).Any(p => p < 0)
            || Abilities.Any(a => string.IsNullOrWhiteSpace(a.Skill) || string.IsNullOrWhiteSpace(a.Ability) || a.Gain < 0 || a.Maximum < 0)
            || Abilities.GroupBy(a => (a.Skill, a.Ability)).Any(g => g.Count() > 1))
            throw new RuleViolation("inheritance.configuration", "Invalid inheritance transfers or ability policy");
    }
}

public static class CommanderInheritance
{
    private static long Transfer(ProgressionState donor, ProgressionRules track, long basis, IReadOnlyList<int> percentages)
    {
        if (donor.Level < 0 || donor.Level >= track.Levels.Count || donor.Experience < 0)
            throw new RuleViolation("inheritance.donor", "Invalid donor progression");
        long total = checked(basis + donor.Experience);
        for (int i = 0; i < donor.Level; i++) total = checked(total + track.Levels[i].ExperienceToNext!.Value);
        int percent = percentages[donor.Level];
        return checked(total / 100 * percent + total % 100 * percent / 100);
    }

    public static CommanderLearningState Apply(CommanderLearningState receiver, CommanderLearningState donor,
        IReadOnlyCollection<string> donorSkills, CommanderInheritanceRules rules)
    {
        rules.Validate();
        if (receiver.Abilities.Concat(donor.Abilities).Any(p => string.IsNullOrWhiteSpace(p.Key) || p.Value < 0)
            || donorSkills.Any(string.IsNullOrWhiteSpace))
            throw new RuleViolation("inheritance.abilities", "Invalid ability or donor skill identities");
        var rank = Progression.Award(receiver.Rank, Transfer(donor.Rank, rules.Rank, rules.RankBase, rules.RankPercent), rules.Rank).State;
        var title = Progression.Award(receiver.Title, Transfer(donor.Title, rules.Title, rules.TitleBase, rules.TitlePercent), rules.Title).State;
        var abilities = receiver.Abilities.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        foreach (var effect in rules.Abilities.Where(a => donorSkills.Contains(a.Skill)))
        {
            int current = abilities.TryGetValue(effect.Ability, out int value) ? value : 0;
            long increased = (long)current + effect.Gain;
            abilities[effect.Ability] = checked((int)(effect.Maximum.HasValue ? Math.Max(current, Math.Min(effect.Maximum.Value, increased)) : increased));
        }
        return new(rank, title, abilities);
    }
}
