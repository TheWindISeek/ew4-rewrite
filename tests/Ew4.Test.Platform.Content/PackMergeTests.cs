using Ew4.Platform.Content;
using Xunit;

namespace Ew4.Test.Platform.Content;

public sealed class PackMergeTests
{
    [Fact]
    public void OpenParent_returns_null_when_manifest_has_no_parent()
    {
        string? baseDir = BasePackDirectory();
        if (baseDir == null)
        {
            return;
        }

        var primary = new DirectoryPackSource(baseDir);
        var repository = new FilePackRepository(Path.GetDirectoryName(baseDir)!);
        Assert.Null(PackMerge.OpenParent(primary, repository));
    }

    [Fact]
    public void OpenParent_opens_base_for_example_overlay_mod()
    {
        string? baseDir = BasePackDirectory();
        string modDir = Path.Combine(ModsRoot(), "ex-campaign-tweak");
        if (baseDir == null || !Directory.Exists(modDir))
        {
            return;
        }

        var primary = new DirectoryPackSource(modDir);
        var repository = new FilePackRepository(Path.GetDirectoryName(baseDir)!);
        IPackSource? parent = PackMerge.OpenParent(primary, repository);
        Assert.NotNull(parent);
        Assert.Equal(PackId.Base, parent!.Manifest.Id);
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

    private static string? BasePackDirectory()
    {
        foreach (string relative in new[] { Path.Combine("install", "web", "mods", "base"), Path.Combine("install", "mods", "base") })
        {
            string install = Path.GetFullPath(relative);
            if (PackManifestPaths.DirectoryHasManifest(install))
            {
                return install;
            }
        }

        string mods = Path.GetFullPath(Path.Combine("mods", "base"));
        return PackManifestPaths.DirectoryHasManifest(mods) ? mods : null;
    }
}
