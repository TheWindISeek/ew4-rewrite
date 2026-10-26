using System.Collections.ObjectModel;
using Ew4.Platform.Engine;

namespace Ew4.Platform.Content.StaticData;

/// <summary>原版技能数值默认表，以及 mod 对其的覆盖。Engine 只接收结果（<see cref="SkillParameters"/>）。</summary>
public static class StaticDataSkillParameters
{
    public static SkillParameters Default { get; } = Defaults();

    private static IReadOnlyDictionary<string, double> Values(params (string key, double value)[] values) =>
        new ReadOnlyDictionary<string, double>(values.ToDictionary(p => p.key, p => p.value, StringComparer.Ordinal));

    private static SkillParameters Defaults()
    {
        var skills = new Dictionary<string, IReadOnlyDictionary<string, double>>(StringComparer.Ordinal);
        void Add(int id, string key, double value) => skills.Add(Ew4Combat.Skill(id), Values((key, value)));
        Add(4, "multiplier", 1.5);
        Add(6, "bonus", 2);
        foreach (int id in new[] { 7, 8, 10, 11, 12, 13 }) Add(id, "bonus", 1);
        Add(16, "multiplier", 0.9);
        Add(18, "bonus", 1);
        Add(19, "multiplier", 1.5);
        Add(20, "multiplier", 1.4);
        Add(21, "multiplier", 1.8);
        Add(23, "multiplier", 1.4);
        Add(24, "multiplier", 1.8);
        foreach (int id in new[] { 25, 26, 27, 28 }) Add(id, "percent", 50);
        Add(30, "percent", 60);
        Add(31, "percent", 10);
        Add(32, "percent", 10);
        for (int id = 33; id <= 39; id++) skills.Add(Ew4Combat.Skill(id), Values(("gain", 1), ("maximum", 5)));
        return new SkillParameters(skills);
    }

    public static SkillParameters FromOverrides(IReadOnlyDictionary<string, Dictionary<string, double>> overrides)
    {
        if (overrides == null) throw new RuleViolation("legacy.skill_parameters", "Missing skill parameter map");
        var skills = Default.Skills.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        foreach (var skill in overrides)
        {
            if (!Default.Skills.TryGetValue(skill.Key, out var defaults) || skill.Value == null)
                throw new RuleViolation("legacy.skill_parameters", "Unsupported original skill: " + skill.Key);
            var values = defaults.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            foreach (var parameter in skill.Value)
            {
                double value = parameter.Value;
                double maximum = parameter.Key == "bonus" ? 1000 : 100;
                if (!defaults.ContainsKey(parameter.Key) || double.IsNaN(value) || double.IsInfinity(value)
                    || value < 0 || value > maximum || parameter.Key != "multiplier" && value != Math.Truncate(value))
                    throw new RuleViolation("legacy.skill_parameters", "Invalid parameter: " + skill.Key + "." + parameter.Key);
                values[parameter.Key] = value;
            }
            skills[skill.Key] = new ReadOnlyDictionary<string, double>(values);
        }
        return new SkillParameters(skills);
    }
}
