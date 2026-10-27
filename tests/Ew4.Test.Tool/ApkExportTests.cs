using Ew4.Tool.Lib.Export;
using Xunit;

namespace Ew4.Test.Tool;

public sealed class ApkExportTests
{
    [Fact]
    public void DryRun_ex_commander_skills_patches_commander_table()
    {
        ApkExportResult result = ApkExporter.Export(Request("ex-commander-skills"));
        Assert.Null(result.OutPath);
        Assert.True(result.FilesPatched >= 1);
    }

    [Fact]
    public void DryRun_ex_conquest_tweak_patches_scenario_btl()
    {
        ApkExportResult result = ApkExporter.Export(Request("ex-conquest-tweak"));
        Assert.True(result.FilesPatched >= 1);
    }

    [Fact]
    public void DryRun_huangkai_merged_overlay_has_asset_patches()
    {
        if (!Directory.Exists(Path.Combine(RepoRoot(), "mods", "huangkai")))
        {
            return;
        }

        ApkExportResult result = ApkExporter.Export(Request("huangkai"));
        Assert.True(result.FilesPatched > 10);
    }

    private static ApkExportRequest Request(string modId) =>
        new(
            OutPath: Path.Combine(Path.GetTempPath(), "ew4-export-test.apk"),
            BaseModDirectory: Path.Combine(RepoRoot(), "mods", "base"),
            ModsDirectory: Path.Combine(RepoRoot(), "mods"),
            RepositoryRoot: RepoRoot(),
            ModId: modId,
            ExportId: 1_730_000_300,
            DryRun: true,
            AllowLossy: true);

    private static string RepoRoot()
    {
        string? dir = Directory.GetCurrentDirectory();
        for (int i = 0; i < 10 && dir != null; i++)
        {
            if (File.Exists(Path.Combine(dir, "Ew4.sln")))
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new InvalidOperationException("Ew4.sln not found");
    }
}
