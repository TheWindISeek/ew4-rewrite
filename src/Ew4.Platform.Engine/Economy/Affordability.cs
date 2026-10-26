using System;

namespace Ew4.Platform.Engine;

public sealed record AffordabilityRules(long? MaximumCopies = null, bool RequireMoneyCost = false);

/// <summary>按资源与单价计算可负担份数；Engine、Content 与 AI 共用。</summary>
public static class Affordability
{
    public static long? Copies(Resources funds, Resources cost, AffordabilityRules? configuration = null)
    {
        cost.Validate();
        var rules = configuration ?? new();
        if (rules.MaximumCopies < 0) throw new RuleViolation("economy.affordability_configuration", "Negative affordability cap");
        if (funds.Money < 0 || funds.Industry < 0 || funds.Food < 0) return 0;
        if (rules.RequireMoneyCost && cost.Money == 0) return 0;
        long? copies = rules.MaximumCopies;
        foreach (var component in new[] { (funds.Money, cost.Money), (funds.Industry, cost.Industry), (funds.Food, cost.Food) })
            if (component.Item2 > 0)
            {
                long count = component.Item1 / component.Item2;
                copies = copies.HasValue ? Math.Min(copies.Value, count) : count;
            }
        return copies;
    }
}
