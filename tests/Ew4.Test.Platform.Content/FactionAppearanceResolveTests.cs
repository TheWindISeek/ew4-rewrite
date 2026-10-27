using Ew4.Platform.Content;
using Ew4.Platform.Engine;
using Xunit;

namespace Ew4.Test.Platform.Content;

public sealed class FactionAppearanceResolveTests
{
    [Fact]
    public void Default_color_is_non_zero_and_stable()
    {
        int first = FactionAppearanceResolve.DefaultPackedColor("faction.dynamic.1");
        int second = FactionAppearanceResolve.DefaultPackedColor("faction.dynamic.1");
        Assert.Equal(first, second);
        Assert.NotEqual(0, first);
        Assert.Equal(255, (first >> 24) & 255);
    }

    [Fact]
    public void Resolve_prefers_country_flag_when_appearance_missing()
    {
        var resolved = FactionAppearanceResolve.Resolve(
            null,
            "fra",
            null,
            "#112233",
            "ger",
            0,
            "scenario.faction.0");
        Assert.Equal("fra", resolved.Flag);
        Assert.Equal(ScenarioFactions.PackedColor("#112233"), resolved.Color);
    }

    [Fact]
    public void PackBattleColor_parses_aarrggbb_and_zero_rgb_means_unset()
    {
        int packed = FactionAppearanceResolve.PackBattleColor(unchecked((int)0x28FFFF00));
        Assert.Equal(40, (packed >> 24) & 255);
        Assert.Equal(255, packed & 255);
        Assert.Equal(255, (packed >> 8) & 255);
        Assert.Equal(0, (packed >> 16) & 255);
        Assert.Equal(unchecked((int)0x28000000), FactionAppearanceResolve.PackBattleColor(unchecked((int)0x28000000)));
    }

    [Fact]
    public void Resolve_keeps_prussia_battle_tint_when_rgb_is_zero()
    {
        var resolved = FactionAppearanceResolve.Resolve(null, null, null, null, "pru", unchecked((int)0x28000000), "scenario.faction.1.pru");
        Assert.Equal(unchecked((int)0x28000000), resolved.Color);
    }

    [Fact]
    public void Resolve_uses_default_color_when_all_sources_missing()
    {
        var resolved = FactionAppearanceResolve.Resolve(null, null, null, null, null, 0, "scenario.faction.3");
        Assert.Equal(FactionAppearanceResolve.DefaultPackedColor("scenario.faction.3"), resolved.Color);
    }
}
