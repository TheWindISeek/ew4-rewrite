using Ew4.Platform.Ai;
using Ew4.Platform.Engine;

namespace Ew4.Platform.Content.StaticData;

/// <summary>
/// 原版 AI 的数值表（战术分值、招募池准入、设施/要塞配方）。
/// 这些是内容数据而非引擎逻辑，因此由 Content 提供，Engine 只定义规则类型。
/// 键使用原版数字 id，由 <see cref="StaticDataAiCatalog"/> 映射为 ew4 id。
/// </summary>
internal static class StaticDataAiDefaults
{
    public static AiTacticalRules Tactics() => new()
    {
        Buildings = new Dictionary<string, AiBuildingValue> { ["0"] = new(41, 20), ["1"] = new(81, 40), ["2"] = new(71, 35), ["3"] = new(61, 30), ["4"] = new(1, 15) },
        Installations = new Dictionary<string, long> { ["0"] = 35, ["1"] = 40, ["2"] = 40 },
        Priorities = new Dictionary<int, long> { [1] = 260, [2] = 200 },
        Grades = new Dictionary<string, float> { ["0"] = 1, ["1"] = 1.3f, ["2"] = 1.6f },
        Modifiers = new AiAttackModifier[]
        {
            new() { AttackerCategories = new[] { "artillery" }, AttackerTrait = "ai.direct_artillery", DefenderCategories = new[] { "infantry", "cavalry" }, Bonus = 250 },
            new() { AttackerCategories = new[] { "artillery" }, AttackerTrait = "ai.direct_artillery", DefenderCategories = new[] { "naval", "fort" }, Bonus = 150 },
            new() { AttackerCategories = new[] { "artillery" }, AttackerTrait = "siege_gun", DefenderCategories = new[] { "fort" }, Bonus = 200 },
            new() { AttackerCategories = new[] { "artillery" }, AttackerTrait = "siege_gun", TargetBuilding = true, PerBuildingLevel = 50 },
            new() { AttackerCategories = new[] { "artillery" }, AttackerTrait = "ai.rocket", Bonus = 500 },
            new() { AttackerCategories = new[] { "infantry", "cavalry" }, DefenderTrait = "ai.counter_mounted", Bonus = -60 },
            new() { AttackerCategories = new[] { "infantry" }, TargetInstallation = "0", Bonus = -60 },
            new() { AttackerCategories = new[] { "cavalry" }, TargetInstallation = "1", Bonus = -30 },
            new() { AttackerCategories = new[] { "infantry", "cavalry" }, DefenderCategories = new[] { "naval" }, Bonus = -30 },
            new() { AttackerCategories = new[] { "naval" }, ExcludedDefenderCategories = new[] { "naval" }, Bonus = 100 },
            new() { AttackerCategories = new[] { "naval" }, ExcludedDefenderCategories = new[] { "naval" }, DefenderTrait = "coastal_gun", Bonus = -250 },
            new() { AttackerCategories = new[] { "fort" }, DefenderTrait = "siege_gun", Bonus = -270 },
            new() { DefenderCategories = new[] { "artillery" }, Bonus = 150 },
            new() { DefenderConstruction = true, Bonus = 400 },
            new() { AttackerCategories = new[] { "infantry", "cavalry", "artillery" }, AttackerAtSea = true, DefenderCategories = new[] { "naval" }, Bonus = -180 },
            new() { AttackerCommander = true, Bonus = 40 },
            new() { DefenderTransport = true, Bonus = 5 },
            new() { TargetInstallation = "2", Bonus = -25 },
            new() { SourceInstallation = "0", Bonus = -20 }, new() { SourceInstallation = "1", Bonus = -20 },
            new() { TargetHuman = true, Bonus = 40 }
        }
    };

    public static AiOfferPoolRules OfferPool() => new()
    {
        SelectionStride = 20, Admission = new Dictionary<string, AiOfferAdmission> { ["33"] = new(10, 8) }
    };

    public static AiInstallationRules Installations() => new()
    {
        Recipes = new AiInstallationRecipe[]
        {
            new("41", new[] { new AiEnemyRandomBonus("infantry", 3) }), new("42", new[] { new AiEnemyRandomBonus("cavalry", 5) }),
            new("43", new[] { new AiEnemyRandomBonus("artillery", 6), new("naval", 6), new("fort", 3) })
        }, PreferenceOrder = new[] { "41", "43", "42" }
    };
    public static AiFortressRules Fortresses() => new()
    {
        Recipes = new AiFortressRecipe[] { new("40", 3, true), new("37", 2, false), new("38", 2, false), new("39", 2, false) }
    };
}
