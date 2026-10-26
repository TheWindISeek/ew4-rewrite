using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Ew4.Platform.Engine;

/// <summary>
/// 原版技能数值（倍率/加成/百分比等）的只读容器。Engine 不内置任何数值：
/// 数值由 Content 提供（默认表与 mod 覆盖见 <c>StaticDataSkillParameters</c>）。
/// </summary>
public sealed class SkillParameters
{
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> Skills { get; }

    /// <summary>空表：未配置时任何查询都会失败，而不是悄悄使用原版默认值。</summary>
    public static SkillParameters None { get; } = new(new Dictionary<string, IReadOnlyDictionary<string, double>>());

    public SkillParameters(IReadOnlyDictionary<string, IReadOnlyDictionary<string, double>> skills) =>
        Skills = new ReadOnlyDictionary<string, IReadOnlyDictionary<string, double>>(
            new Dictionary<string, IReadOnlyDictionary<string, double>>(skills, StringComparer.Ordinal));

    public float Multiplier(int skill) => (float)Skills[Ew4Combat.Skill(skill)]["multiplier"];
    public int Bonus(int skill) => (int)Skills[Ew4Combat.Skill(skill)]["bonus"];
    public int Percent(int skill) => (int)Skills[Ew4Combat.Skill(skill)]["percent"];
    public int Gain(int skill) => (int)Skills[Ew4Combat.Skill(skill)]["gain"];
    public int Maximum(int skill) => (int)Skills[Ew4Combat.Skill(skill)]["maximum"];
}
