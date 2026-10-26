using Ew4.Platform.Content;
using Xunit;

namespace Ew4.Test.Platform.Content;

public sealed class ModMergeTests
{
    [Fact]
    public void OpenParent_returns_null_when_manifest_has_no_parent()
    {
        string? baseDir = BaseModDirectory();
        if (baseDir == null)
        {
            return;
        }

        var primary = new DirectoryModSource(baseDir);
        var repository = new FileModRepository(Path.GetDirectoryName(baseDir)!);
        Assert.Null(ModMerge.OpenParent(primary, repository));
    }

    [Fact]
    public void OpenParent_opens_base_for_example_overlay_mod()
    {
        string? baseDir = BaseModDirectory();
        string modDir = Path.Combine(ModsRoot(), "ex-campaign-tweak");
        if (baseDir == null || !Directory.Exists(modDir))
        {
            return;
        }

        var primary = new DirectoryModSource(modDir);
        var repository = new FileModRepository(Path.GetDirectoryName(baseDir)!);
        IModSource? parent = ModMerge.OpenParent(primary, repository);
        Assert.NotNull(parent);
        Assert.Equal(ModId.Base, parent!.Manifest.Id);
    }

    private static string ModsRoot() => Path.Combine(RepositoryRoot(), "mods");

    private static string RepositoryRoot()
    {
        string? dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "Ew4.sln")))
            {
                return dir;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        return Directory.GetCurrentDirectory();
    }

    private static string? BaseModDirectory()
    {
        foreach (string relative in new[] { Path.Combine("install", "web", "mods", "base"), Path.Combine("install", "mods", "base") })
        {
            string install = Path.GetFullPath(relative);
            if (ModManifestPaths.DirectoryHasManifest(install))
            {
                return install;
            }
        }

        string mods = Path.GetFullPath(Path.Combine("mods", "base"));
        return ModManifestPaths.DirectoryHasManifest(mods) ? mods : null;
    }
}
