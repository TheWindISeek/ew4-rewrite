using System.Text;
using System.Xml.Linq;
using Ew4.Platform.Content;
using Xunit;

namespace Ew4.Test.Platform.Content;

public sealed class LegacyBattleListScenariosTests
{
    [Fact]
    public void AddIds_includes_battlelist_entries_with_merged_legacy_btl()
    {
        string root = Path.GetFullPath("mods");
        string greatFront = Path.Combine(root, "great-front");
        string baseDir = Path.Combine(root, "base");
        if (!Directory.Exists(greatFront) || !Directory.Exists(baseDir))
        {
            return;
        }

        IPackSource merged = new MergedPackSource(new DirectoryPackSource(greatFront), new DirectoryPackSource(baseDir));
        var ids = new HashSet<string>(StringComparer.Ordinal);
        LegacyBattleListScenarios.AddIds(merged, ids);
        Assert.Contains("campaign3_01", ids);
        Assert.Contains("campaign1_01", ids);
        Assert.True(ids.Count >= 80);
    }

    [Fact]
    public void ParseUtf8Xml_accepts_utf8_bom_battlelist()
    {
        byte[] bytes = Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes("<battlelist><battle file=\"a.btl\"/></battlelist>"))
            .ToArray();
        XDocument document = LegacyBattleListScenarios.ParseUtf8Xml(bytes);
        Assert.Equal("battlelist", document.Root!.Name.LocalName);
    }
}
