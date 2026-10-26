using Ew4.Platform.Engine;
using Xunit;

namespace Ew4.Test.Platform.Engine;

/// <summary>
/// 规则配置的合法性只在 <see cref="RuleSet.Validate"/> 校验一次；
/// 规则函数本身不再逐次校验（见 RuleSet 注释）。
/// </summary>
public sealed class RuleSetValidationTests
{
    private static RuleSet MinimalValid() => new(
        Turns: new WorldTurnRules(),
        Movement: new WorldMoveRules(),
        Battle: new WorldBattleRules(),
        Recruitment: new Dictionary<string, IReadOnlyDictionary<string, RecruitmentOffer>>(),
        Commanders: new WorldCommanderRules(),
        Training: new TrainingRules { Stages = new[] { new TrainingStage("0", null) } },
        Upgrades: new BuildingUpgradeRules(),
        Installations: new Dictionary<string, InstallationBuildRules>(),
        Transport: new TransportPurchaseRules(),
        Disband: new DisbandRules(),
        Compat: Array.Empty<string>());

    [Fact]
    public void Minimal_rules_are_valid() => MinimalValid().Validate();

    [Fact]
    public void Training_without_stages_is_rejected_by_rule_set()
    {
        var rules = MinimalValid() with { Training = new TrainingRules() };
        Assert.Throws<RuleViolation>(rules.Validate);
    }

    [Fact]
    public void Invalid_battle_probability_is_rejected_by_rule_set()
    {
        var rules = MinimalValid() with { Battle = new WorldBattleRules { KillMoraleProbability = 101 } };
        Assert.Throws<RuleViolation>(rules.Validate);
    }
}
