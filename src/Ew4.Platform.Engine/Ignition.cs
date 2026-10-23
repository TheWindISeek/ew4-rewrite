using System;
using System.Collections.Generic;
using System.Linq;

namespace Ew4.Platform.Engine;

public sealed record FireExposure
{
    public bool Sea { get; init; }
    public bool Occupied { get; init; }
    public bool Naval { get; init; }
    public bool Transport { get; init; }
    public bool Immune { get; init; }
    public int Environment { get; init; }
    public int Burning { get; init; }
}
public sealed record IgnitionResult(bool Triggered, int Environment, int Burning, ulong RandomState);

public static class Ignition
{
    public static IgnitionResult Resolve(FireExposure target, bool incendiary,
        IReadOnlyList<int> intensityProbabilities, Ew4Random random)
    {
        if (target.Environment < 0 || target.Burning < 0 || intensityProbabilities.Any(p => p < 0)
            || intensityProbabilities.Sum(p => (long)p) > 100)
            throw new RuleViolation("fire.configuration", "Invalid environment or fire probabilities");
        int environment = target.Environment, burning = target.Burning;
        bool eligible = incendiary && (target.Sea ? !target.Occupied || burning == 0 : environment == 0)
            && !(target.Occupied && target.Immune);
        if (!eligible) return new IgnitionResult(false, environment, burning, random.State);
        int roll = random.Next(100), threshold = 0;
        for (int i = 0; i < intensityProbabilities.Count; i++)
        {
            threshold += intensityProbabilities[i];
            if (roll >= threshold) continue;
            if (!target.Sea) environment = i + 1;
            else if (target.Occupied && (target.Naval || target.Transport)) burning = i + 1;
            return new IgnitionResult(true, environment, burning, random.State);
        }
        return new IgnitionResult(false, environment, burning, random.State);
    }
}
