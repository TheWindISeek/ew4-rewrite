using System.Collections.Frozen;
using Ew4.Platform.Mod.Apk;
using Ew4.Platform.Persistence.Packages;
using Ew4.Tool.Lib.Export;
using Xunit;

namespace Ew4.Test.Tool;

public sealed class ApkAssetPathMappingTests
{
    [Fact]
    public void Shell_index_roundtrips_btl_file_name()
    {
        var baseline = new DirectoryModSource(FindModsBase());
        IReadOnlyList<string> shell = ["assets/campaign1_01.btl", "assets/tables/def_commander.xml"];
        FrozenDictionary<string, string> index = ApkAssetPathMapping.BuildShellIndex(baseline, shell);
        string? logical = ModApkPathMapping.ResolveModPath(baseline, "assets/campaign1_01.btl");
        Assert.Equal("source/original/campaign1_01.btl", logical);
        Assert.True(index.TryGetValue("source/original/campaign1_01.btl", out string? apk));
        Assert.Equal("assets/campaign1_01.btl", apk);
    }

    private static string FindModsBase()
    {
        string? dir = Directory.GetCurrentDirectory();
        for (int i = 0; i < 8 && dir != null; i++)
        {
            string candidate = Path.Combine(dir, "mods", "base");
            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new InvalidOperationException("mods/base not found");
    }
}
