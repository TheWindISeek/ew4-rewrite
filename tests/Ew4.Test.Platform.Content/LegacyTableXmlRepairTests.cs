using System.Text;
using Ew4.Platform.Content.Build;
using Xunit;

namespace Ew4.Test.Platform.Content;

public sealed class LegacyTableXmlRepairTests
{
    [Fact]
    public void NormalizeCommanderTable_fixes_missing_attribute_space_and_non_numeric_stats()
    {
        string xml = """
            <commanders>
              <commander id="1" name="Test" country="fra" rank="8" nobilityrank="3"star="4" infantry="1" warship="w" movement="2" training="0" skill1="0" skill2="-1" item1="-1" item2="-1" drawlots="0"/>
            </commanders>
            """;
        byte[] normalized = LegacyTableXmlRepair.NormalizeCommanderTable(Encoding.UTF8.GetBytes(xml));
        string text = Encoding.UTF8.GetString(normalized);
        Assert.Contains("nobilityrank=\"3\" star=\"4\"", text, StringComparison.Ordinal);
        Assert.Contains("warship=\"0\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void SanitizeIntegerAttribute_returns_fallback_for_garbage()
    {
        Assert.Equal("0", LegacyTableXmlRepair.SanitizeIntegerAttribute("w"));
        Assert.Equal("14", LegacyTableXmlRepair.SanitizeIntegerAttribute("14"));
    }

    [Fact]
    public void NormalizeCommanderTable_clamps_negative_unit_stars_to_zero()
    {
        string xml = """
            <commanders>
              <commander id="30001" name="Schwarzenberg" country="aus" rank="14" nobilityrank="7" star="5" infantry="4" cavalry="-2" artillery="3" warship="1" fort="3" movement="1" training="4" skill1="0" item1="-1" item2="-1" drawlots="0"/>
            </commanders>
            """;
        byte[] normalized = LegacyTableXmlRepair.NormalizeCommanderTable(Encoding.UTF8.GetBytes(xml));
        string text = Encoding.UTF8.GetString(normalized);
        Assert.Contains("cavalry=\"0\"", text, StringComparison.Ordinal);
    }
}
