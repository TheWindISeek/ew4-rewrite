using Ew4.Platform.Engine;
using Xunit;

namespace Ew4.Test.Platform.Engine;

public sealed class HazardAndSurvivalTests
{
    [Fact]
    public void Damage_is_percentage_of_maximum()
    {
        Assert.Equal(96, Hazards.Damage(100, 100, 4));
        Assert.Equal(0, Hazards.Damage(0, 100, 50));
    }

    [Fact]
    public void Nonlethal_damage_leaves_one_health()
    {
        Assert.Equal(1, Hazards.Damage(3, 100, 50));
        Assert.Equal(0, Hazards.Damage(3, 100, 50, nonlethal: false));
    }

    [Theory]
    [InlineData(101, 100, 10)]
    [InlineData(10, 100, 101)]
    [InlineData(10, 0, 10)]
    public void Damage_rejects_invalid_inputs(int health, int maximum, int percentage) =>
        Assert.Throws<RuleViolation>(() => Hazards.Damage(health, maximum, percentage));

    [Fact]
    public void Burning_stage_one_always_decays_to_zero_and_hurts()
    {
        var rules = Hazards.DefaultBurning();
        var result = Hazards.EndUnit(100, 100, 1, new Ew4Random(1), rules);
        Assert.Equal(96, result.Health);
        Assert.Equal(0, result.Intensity);
        Assert.True(result.Acted);
    }

    [Fact]
    public void No_intensity_means_no_damage_and_no_decay()
    {
        var result = Hazards.EndUnit(80, 100, 0, new Ew4Random(1), Hazards.DefaultBurning());
        Assert.Equal(80, result.Health);
        Assert.Equal(0, result.Intensity);
    }

    [Fact]
    public void Unknown_intensity_is_rejected() =>
        Assert.Throws<RuleViolation>(() => Hazards.EndUnit(80, 100, 9, new Ew4Random(1), Hazards.DefaultBurning()));

    [Fact]
    public void Decay_uses_only_listed_transitions()
    {
        var rules = Hazards.DefaultBurning();
        for (ulong seed = 0; seed < 50; seed++)
            Assert.InRange(Hazards.Decay(3, new Ew4Random(seed), rules), 0, 2);
    }

    [Fact]
    public void Survival_units_only_ignores_buildings()
    {
        var rules = new SurvivalRules(Units: true, Buildings: false, new[] { "farmland" });
        Assert.True(FactionSurvival.Defeated(new[] { new SurvivalAsset(false, "city") }, rules));
        Assert.False(FactionSurvival.Defeated(new[] { new SurvivalAsset(true, null) }, rules));
    }

    [Fact]
    public void Survival_buildings_only_ignores_farmland_and_units()
    {
        var rules = new SurvivalRules(Units: false, Buildings: true, new[] { "farmland" });
        Assert.True(FactionSurvival.Defeated(new[] { new SurvivalAsset(true, null), new SurvivalAsset(false, "farmland") }, rules));
        Assert.False(FactionSurvival.Defeated(new[] { new SurvivalAsset(false, "city") }, rules));
    }

    [Fact]
    public void Survival_default_counts_units_or_buildings()
    {
        var rules = SurvivalRules.Default;
        Assert.False(FactionSurvival.Defeated(new[] { new SurvivalAsset(true, null) }, rules));
        Assert.False(FactionSurvival.Defeated(new[] { new SurvivalAsset(false, "city") }, rules));
        Assert.True(FactionSurvival.Defeated(Array.Empty<SurvivalAsset>(), rules));
    }
}
