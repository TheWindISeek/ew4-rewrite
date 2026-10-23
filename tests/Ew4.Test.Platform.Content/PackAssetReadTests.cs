using Ew4.Platform.Content;
using Xunit;

namespace Ew4.Test.Platform.Content;

public sealed class PackAssetReadTests
{
    [Fact]
    public void Resolve_prefers_child_tables_over_parent_original_for_def_xml()
    {
        string root = Path.GetFullPath(Path.Combine("mods"));
        string ww1 = Path.Combine(root, "ww1");
        string baseDir = Path.Combine(root, "base");
        if (!Directory.Exists(ww1) || !Directory.Exists(baseDir))
        {
            return;
        }

        IPackSource merged = new MergedPackSource(new DirectoryPackSource(ww1), new DirectoryPackSource(baseDir));
        bool ok = PackAssetReader.TryResolve(merged, "original/def_battlelist.xml", out string packPath);
        Assert.True(ok);
        Assert.StartsWith("assets/tables/def_battlelist.xml", packPath, StringComparison.Ordinal);
        byte[] bytes = merged.Read(packPath);
        string text = System.Text.Encoding.UTF8.GetString(bytes);
        Assert.Contains("conquest_axis_1.btl", text, StringComparison.Ordinal);
    }
}
