using System;
using System.Globalization;
using System.Linq;
using Ew4.Platform.Engine;

namespace Ew4.Platform.Content;

public static class EngineRequirement
{
    public static void AssertCompatible(PackRequirements requires, EngineInfo engine)
    {
        if (!Satisfies(requires.Engine, engine.EngineVersion))
        {
            throw new ContentError(
                "mod.engine",
                $"Pack requires engine {requires.Engine}; current engine is {engine.EngineVersion}.");
        }

        foreach (string capability in requires.Capabilities)
        {
            if (!engine.Capabilities.Contains(capability))
            {
                throw new ContentError(
                    "mod.capability",
                    $"Pack requires capability '{capability}' which this engine does not provide.");
            }
        }
    }

    private static bool Satisfies(string requirement, string engineVersion)
    {
        foreach (string clause in requirement.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (!SatisfiesClause(clause.Trim(), engineVersion))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SatisfiesClause(string clause, string engineVersion)
    {
        if (clause.StartsWith(">=", StringComparison.Ordinal))
        {
            string needed = clause[2..].Trim();
            return CompareVersions(engineVersion, needed) >= 0;
        }

        if (clause.StartsWith("<", StringComparison.Ordinal))
        {
            string needed = clause[1..].Trim();
            return CompareVersions(engineVersion, needed) < 0;
        }

        if (clause.StartsWith("=", StringComparison.Ordinal))
        {
            string needed = clause[1..].Trim();
            return CompareVersions(engineVersion, needed) == 0;
        }

        return string.Equals(clause, engineVersion, StringComparison.OrdinalIgnoreCase);
    }

    private static int CompareVersions(string left, string right)
    {
        var leftParts = left.Split('.', StringSplitOptions.RemoveEmptyEntries);
        var rightParts = right.Split('.', StringSplitOptions.RemoveEmptyEntries);
        int length = Math.Max(leftParts.Length, rightParts.Length);
        for (int i = 0; i < length; i++)
        {
            int l = i < leftParts.Length && int.TryParse(leftParts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int lv)
                ? lv
                : 0;
            int r = i < rightParts.Length && int.TryParse(rightParts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out int rv)
                ? rv
                : 0;
            if (l != r)
            {
                return l.CompareTo(r);
            }
        }

        return 0;
    }
}
