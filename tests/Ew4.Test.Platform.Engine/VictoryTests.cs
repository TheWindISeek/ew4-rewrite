using Ew4.Platform.Engine;
using Xunit;

namespace Ew4.Test.Platform.Engine;

public sealed class VictoryTests
{
    private static FactionFacts Player(bool defeated = false, int objectives = 0) =>
        new("p", "a", Human: true, Defeated: defeated, Objectives: objectives);

    private static FactionFacts Enemy(bool defeated = false, int objectives = 0) =>
        new("e", "b", Human: false, Defeated: defeated, Objectives: objectives);

    private static BattleOutcome Evaluate(int round, VictoryRules rules, params FactionFacts[] factions) =>
        Victory.Evaluate("a", round, factions, rules);

    [Fact]
    public void Victory_when_no_enemy_camp_remains() =>
        Assert.Equal(BattleOutcome.Victory, Evaluate(3, new(), Player(), Enemy(defeated: true)));

    [Fact]
    public void Defeat_when_player_camp_has_no_living_human() =>
        Assert.Equal(BattleOutcome.Defeat, Evaluate(3, new(), Player(defeated: true), Enemy()));

    [Fact]
    public void Continue_when_both_sides_alive_and_objectives_disabled() =>
        Assert.Equal(BattleOutcome.Continue, Evaluate(3, new(), Player(), Enemy()));

    [Fact]
    public void Defeat_after_round_limit_even_if_winning()
    {
        var rules = new VictoryRules(RoundLimit: 10);
        Assert.Equal(BattleOutcome.Continue, Evaluate(10, rules, Player(), Enemy()));
        Assert.Equal(BattleOutcome.Defeat, Evaluate(11, rules, Player(), Enemy(defeated: true)));
    }

    [Theory]
    [InlineData(1, 0, BattleOutcome.Victory)]   // 敌方目标清零 → 胜利
    [InlineData(0, 1, BattleOutcome.Defeat)]    // 己方目标清零且敌方仍有 → 失败
    [InlineData(2, 2, BattleOutcome.Continue)]
    public void Objectives_decide_outcome_when_enabled(int own, int enemy, BattleOutcome expected)
    {
        var rules = new VictoryRules(ObjectivesEnabled: true);
        Assert.Equal(expected, Evaluate(3, rules, Player(objectives: own), Enemy(objectives: enemy)));
    }

    [Fact]
    public void Duplicate_faction_ids_are_rejected() =>
        Assert.Throws<RuleViolation>(() => Evaluate(1, new(), Player(), Player()));

    [Fact]
    public void Round_below_one_is_rejected() =>
        Assert.Throws<RuleViolation>(() => Evaluate(0, new(), Player(), Enemy()));
}
