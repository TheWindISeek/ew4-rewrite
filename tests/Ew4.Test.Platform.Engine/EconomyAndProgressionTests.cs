using Ew4.Platform.Engine;
using Xunit;

namespace Ew4.Test.Platform.Engine;

public sealed class EconomyAndProgressionTests
{
    [Fact]
    public void Round_one_keeps_current_resources()
    {
        var result = TurnEconomy.Start(1, new(10, 10, 10), new(5, 5, 5), foodConsumption: 3);
        Assert.Equal(new Resources(10, 10, 10), result.Resources);
        Assert.Equal(0, result.Shortage);
    }

    [Fact]
    public void Later_rounds_add_income_and_pay_food()
    {
        var result = TurnEconomy.Start(2, new(10, 10, 10), new(5, 5, 5), foodConsumption: 4);
        Assert.Equal(new Resources(15, 15, 11), result.Resources);
        Assert.Equal(0, result.Shortage);
    }

    [Fact]
    public void Resource_cap_clamps_income()
    {
        var result = TurnEconomy.Start(2, new(10, 10, 10), new(5, 5, 5), foodConsumption: 0, resourceCap: 12);
        Assert.Equal(new Resources(12, 12, 12), result.Resources);
    }

    [Theory]
    [InlineData(15, 1)] // 15/20 = .75
    [InlineData(10, 2)] // .5
    [InlineData(5, 3)]  // .25
    [InlineData(1, 4)]
    public void Food_shortage_level_follows_coverage_ratio(long food, int shortage)
    {
        var result = TurnEconomy.Start(2, new(0, 0, 0), new(0, 0, food), foodConsumption: 20);
        Assert.Equal(shortage, result.Shortage);
        Assert.Equal(0, result.Resources.Food);
    }

    [Fact]
    public void Negative_resources_are_rejected() =>
        Assert.Throws<RuleViolation>(() => TurnEconomy.Start(2, new(-1, 0, 0), new(0, 0, 0), 0));

    private static ProgressionRules Rules(bool single = false, bool accumulate = false, int healing = 0) => new()
    {
        Levels = new[]
        {
            new ProgressionLevel("0", 100),
            new ProgressionLevel("1", 200, healing),
            new ProgressionLevel("2", null),
        },
        SingleAdvancePerAward = single,
        AccumulateAtCap = accumulate,
    };

    [Fact]
    public void Award_advances_one_level_and_keeps_remainder()
    {
        var result = Progression.Award(new(0, 0), 150, Rules());
        Assert.Equal(new ProgressionState(1, 50), result.State);
        Assert.Equal(1, result.LevelsGained);
    }

    [Fact]
    public void Large_award_crosses_levels_and_discards_xp_at_cap()
    {
        var result = Progression.Award(new(0, 0), 1000, Rules());
        Assert.Equal(new ProgressionState(2, 0), result.State);
        Assert.Equal(2, result.LevelsGained);
    }

    [Fact]
    public void Single_advance_stops_after_first_level()
    {
        var result = Progression.Award(new(0, 0), 1000, Rules(single: true));
        Assert.Equal(new ProgressionState(1, 900), result.State);
    }

    [Fact]
    public void Entering_a_level_reports_healing()
    {
        var result = Progression.Award(new(0, 0), 100, Rules(healing: 5));
        Assert.Equal(5, result.Healing);
    }

    [Fact]
    public void At_cap_award_is_ignored_unless_accumulating()
    {
        var capped = new ProgressionState(2, 0);
        Assert.Equal(capped, Progression.Award(capped, 500, Rules()).State);
        Assert.Equal(new ProgressionState(2, 500), Progression.Award(capped, 500, Rules(accumulate: true)).State);
    }

    [Fact]
    public void Invalid_ladders_and_states_are_rejected()
    {
        var capHasCost = new ProgressionRules { Levels = new[] { new ProgressionLevel("0", 100) } };
        Assert.Throws<RuleViolation>(() => Progression.Award(new(0, 0), 1, capHasCost));
        Assert.Throws<RuleViolation>(() => Progression.Award(new(5, 0), 1, Rules()));
        Assert.Throws<RuleViolation>(() => Progression.Award(new(0, 0), -1, Rules()));
    }
}
