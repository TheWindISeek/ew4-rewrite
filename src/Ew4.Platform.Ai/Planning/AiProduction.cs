using System;
using System.Collections.Generic;
using System.Linq;

namespace Ew4.Platform.Ai;

using Ew4.Platform.Engine;

public sealed record AiRecruitEconomyBand(long MinimumSurplus, int ReserveDivisor, int Bonus);
public sealed record AiRecruitPressureBand(float MinimumStrengthRatio, int SkipChance);
public sealed record AiRecruitRules
{
    public IReadOnlyList<AiRecruitEconomyBand> Economy { get; init; } = new AiRecruitEconomyBand[]
    {
        new(long.MinValue, 4, -2), new(-40, 4, -1), new(-39, 3, -1), new(0, 3, 1),
        new(1, 2, 1), new(40, 2, 2), new(41, 1, 2)
    };
    public IReadOnlyList<int> TierAdmissionChance { get; init; } = new[] { 31, 51, 71, 91 };
    public IReadOnlyList<AiRecruitPressureBand> Pressure { get; init; } = new AiRecruitPressureBand[]
    {
        new(0, 0), new(.25f, 21), new(.5f, 41), new(.75f, 56), new(1, 71)
    };
    public long LowValueThreshold { get; init; } = 50;
    public int LowValueSkipChance { get; init; } = 50;
    public void Validate()
    {
        if (Economy.Count == 0 || Economy[0].MinimumSurplus != long.MinValue || Economy.Any(b => b.ReserveDivisor < 1)
            || Economy.Zip(Economy.Skip(1), (a, b) => a.MinimumSurplus >= b.MinimumSurplus).Any(v => v)
            || TierAdmissionChance.Any(p => p < 0 || p > 100) || Pressure.Count == 0 || Pressure[0].MinimumStrengthRatio != 0
            || Pressure.Any(p => float.IsNaN(p.MinimumStrengthRatio) || float.IsInfinity(p.MinimumStrengthRatio)
                || p.MinimumStrengthRatio < 0 || p.SkipChance < 0 || p.SkipChance > 100)
            || Pressure.Zip(Pressure.Skip(1), (a, b) => a.MinimumStrengthRatio >= b.MinimumStrengthRatio).Any(v => v)
            || LowValueThreshold < 0 || LowValueSkipChance < 0 || LowValueSkipChance > 100)
            throw new RuleViolation("ai.recruit_configuration", "Invalid recruitment economy or pressure policy");
    }
}
public sealed record AiRecruitSite(int Cell, bool Eligible, bool Occupied, bool Mobile, bool Stunned, bool HoldPosition,
    int? Relocation, long LocationValue, long Strength, long Threat, int Environment);
public sealed record AiRecruitChoice(int Cell, int? Relocation, long Deficit);
public sealed record AiUpgradeSite(int Cell, string? BuildingKind, int Environment, long EnemyStrength, long FriendlyStrength, bool CanUpgrade);
public sealed record AiUpgradeChance(long MinimumWeight, int AdmissionChance);
public sealed record AiUpgradeRules
{
    public IReadOnlyDictionary<string, long> BuildingWeights { get; init; } = new Dictionary<string, long>
        { ["0"] = 3, ["1"] = 3, ["2"] = 3, ["3"] = 2, ["4"] = 1 };
    public IReadOnlyList<AiUpgradeChance> Chances { get; init; } = new AiUpgradeChance[]
        { new(0, 21), new(10, 31), new(20, 41), new(30, 51), new(40, 61) };
    public void Validate()
    {
        if (BuildingWeights.Any(p => string.IsNullOrWhiteSpace(p.Key) || p.Value < 0) || Chances.Count == 0 || Chances[0].MinimumWeight != 0
            || Chances.Any(p => p.MinimumWeight < 0 || p.AdmissionChance < 0 || p.AdmissionChance > 100)
            || Chances.Zip(Chances.Skip(1), (a, b) => a.MinimumWeight >= b.MinimumWeight).Any(v => v))
            throw new RuleViolation("ai.upgrade_configuration", "Invalid upgrade weight or admission policy");
    }
}
public sealed record AiTransportSite(int Cell, bool HasUnit, bool Transport, bool Sea, bool Coastal, bool CategoryAllowed, int Environment, int SeaNeighbors);
public sealed record AiTaskWeight(string Id, long Weight);

public static partial class AiProduction
{
    public static string? SelectTask(IReadOnlyList<AiTaskWeight> tasks, Func<long, long> draw)
    {
        if (tasks.Any(t => string.IsNullOrWhiteSpace(t.Id) || t.Weight < 0)
            || tasks.Select(t => t.Id).Distinct(StringComparer.Ordinal).Count() != tasks.Count)
            throw new RuleViolation("ai.production_tasks", "Invalid task identities or weights");
        long total = 0;
        foreach (var task in tasks) total = checked(total + task.Weight);
        if (total == 0) return null;
        long selection = Draw(draw, total);
        foreach (var task in tasks)
        {
            if (selection < task.Weight) return task.Id;
            selection -= task.Weight;
        }
        throw new RuleViolation("ai.production_tasks", "Task weights did not cover sampling interval");
    }

    private static long Draw(Func<long, long> draw, long bound)
    {
        long value = draw(bound);
        if (bound < 1 || value < 0 || value >= bound) throw new RuleViolation("ai.production_random", "Invalid production draw");
        return value;
    }

    public static bool AttemptRecruit(long food, long income, long consumption, long draw, AiRecruitRules rules)
    {
        if (food < 0 || income < 0 || consumption < 0 || draw < 0 || draw >= 100)
            throw new RuleViolation("ai.recruit_economy", "Invalid recruitment economy facts");
        long surplus = income - consumption;
        var band = rules.Economy.Last(b => b.MinimumSurplus <= surplus);
        long reserves = consumption == 0 ? food : food / consumption;
        long quotient = reserves / band.ReserveDivisor;
        if (band.Bonus > 0 && quotient > long.MaxValue - band.Bonus) return true;
        long tier = quotient + band.Bonus;
        return tier >= 0 && (tier >= rules.TierAdmissionChance.Count || draw < rules.TierAdmissionChance[(int)tier]);
    }

    public static AiRecruitChoice? SelectRecruitSite(IReadOnlyList<AiRecruitSite> sites, AiRecruitRules rules, Func<long, long> draw)
    {
        AiRecruitChoice? best = null;
        foreach (var site in sites)
        {
            if (site.Cell < 0 || site.Relocation < 0 || site.LocationValue < 0 || site.Strength < 0 || site.Threat < 0 || site.Environment < 0)
                throw new RuleViolation("ai.recruit_site", "Invalid recruitment site facts");
            if (!site.Eligible || site.Occupied && (!site.Mobile || site.Stunned || site.HoldPosition || site.Relocation == null)) continue;
            long strength = site.Strength;
            long need = checked(site.LocationValue + site.Threat);
            if (site.Threat == 0 && site.Environment != 0) strength /= 2;
            var band = rules.Pressure.Last(b => (float)strength >= (float)need * b.MinimumStrengthRatio);
            long sample = Draw(draw, 100);
            bool skip = sample < band.SkipChance;
            bool low = site.LocationValue < rules.LowValueThreshold && site.Threat == 0 && strength != 0;
            long extra = low ? Draw(draw, 100) : 99;
            long deficit = checked(need - strength);
            if (!skip && (!low || extra >= rules.LowValueSkipChance) && deficit >= 0 && (best == null || deficit > best.Deficit))
                best = new(site.Cell, site.Occupied ? site.Relocation : null, deficit);
        }
        return best;
    }

    public static int? SelectUpgrade(IReadOnlyList<AiUpgradeSite> sites, AiUpgradeRules rules, Func<long, long> draw)
    {
        var choices = new List<int>();
        long weight = 0;
        foreach (var site in sites)
        {
            if (site.Cell < 0 || site.Environment < 0 || site.EnemyStrength < 0 || site.FriendlyStrength < 0)
                throw new RuleViolation("ai.upgrade_site", "Invalid upgrade site facts");
            if (site.BuildingKind == null || site.Environment > 0) continue;
            long enemy = site.EnemyStrength, friendly = site.FriendlyStrength;
            // Division with remainder avoids overflowing enemy*4 at modern resource scales.
            bool MoreThan(int multiplier) => friendly / multiplier > enemy || friendly / multiplier == enemy && friendly % multiplier > 0;
            bool accept = MoreThan(4) || (MoreThan(3) ? Draw(draw, 10) > 1 : MoreThan(2) ? Draw(draw, 10) > 2
                : MoreThan(1) ? Draw(draw, 10) > 3 : friendly == enemy && Draw(draw, 10) > 4);
            if (!accept || !site.CanUpgrade) continue;
            if (!rules.BuildingWeights.TryGetValue(site.BuildingKind, out long value)) throw new RuleViolation("ai.upgrade_building", "Unknown upgrade building weight");
            weight = checked(weight + value);
            choices.Add(site.Cell);
        }
        if (choices.Count == 0) return null;
        var chance = rules.Chances.Last(c => c.MinimumWeight <= weight);
        return Draw(draw, 100) < chance.AdmissionChance ? choices[(int)Draw(draw, choices.Count)] : null;
    }

    public static int? SelectTransport(IReadOnlyList<AiTransportSite> sites, bool enabled)
    {
        AiTransportSite? best = null;
        foreach (var site in sites)
        {
            if (site.Cell < 0 || site.Environment < 0 || site.SeaNeighbors < 0) throw new RuleViolation("ai.transport_site", "Invalid transport site facts");
            if (!enabled || !site.HasUnit || site.Transport || site.Sea || !site.Coastal || !site.CategoryAllowed || site.Environment > 0 || site.SeaNeighbors == 0) continue;
            if (best == null || site.SeaNeighbors > best.SeaNeighbors) best = site;
        }
        return best?.Cell;
    }
}
