using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Ew4.Core;

public sealed class LegacySkillParameters
{
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> Skills { get; }
    public static LegacySkillParameters Default { get; } = Defaults();

    private LegacySkillParameters(Dictionary<string, IReadOnlyDictionary<string, double>> skills) =>
        Skills = new ReadOnlyDictionary<string, IReadOnlyDictionary<string, double>>(skills);

    private static IReadOnlyDictionary<string, double> Values(params (string key, double value)[] values) =>
        new ReadOnlyDictionary<string, double>(values.ToDictionary(p => p.key, p => p.value, StringComparer.Ordinal));

    private static LegacySkillParameters Defaults()
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
        return new(skills);
    }

    public static LegacySkillParameters FromOverrides(IReadOnlyDictionary<string, Dictionary<string, double>> overrides)
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
        return new(skills);
    }

    public float Multiplier(int skill) => (float)Skills[Ew4Combat.Skill(skill)]["multiplier"];
    public int Bonus(int skill) => (int)Skills[Ew4Combat.Skill(skill)]["bonus"];
    public int Percent(int skill) => (int)Skills[Ew4Combat.Skill(skill)]["percent"];
    public int Gain(int skill) => (int)Skills[Ew4Combat.Skill(skill)]["gain"];
    public int Maximum(int skill) => (int)Skills[Ew4Combat.Skill(skill)]["maximum"];
}
