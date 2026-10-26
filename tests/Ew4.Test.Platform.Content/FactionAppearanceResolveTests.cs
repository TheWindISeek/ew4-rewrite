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
    public void Resolve_uses_default_color_when_all_sources_missing()
    {
        var resolved = FactionAppearanceResolve.Resolve(null, null, null, null, null, 0, "scenario.faction.3");
        Assert.Equal(FactionAppearanceResolve.DefaultPackedColor("scenario.faction.3"), resolved.Color);
    }
}
