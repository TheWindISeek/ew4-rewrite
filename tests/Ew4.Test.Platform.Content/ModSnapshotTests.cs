using Ew4.Platform.Content;
using Ew4.Platform.Engine;
using Xunit;

namespace Ew4.Test.Platform.Content;

public sealed class ModSnapshotTests
{
    private static readonly IEngine Engine = new global::Ew4.Platform.Engine.Engine();

    [Fact]
    public void Loader_matches_resolver_fingerprint_for_base()
    {
        string? baseDir = BasePackDirectory();
        if (baseDir == null)
        {
            return;
        }

        var primary = new DirectoryPackSource(baseDir);
        var resolver = new ContentResolver(ContentKindRegistry.CreateDefault(), Engine);
        var loader = new ModSnapshotLoader(resolver, new ContentValidator(ContentKindRegistry.CreateDefault()));

        ResolvedContent direct = resolver.Resolve(primary, null);
        ResolvedContent loaded = loader.LoadActivatedView(primary, null);

        Assert.Equal(direct.Fingerprint, loaded.Fingerprint);
        Assert.Equal(direct.Manifest.Id, loaded.Manifest.Id);
    }

    [Fact]
    public void Snapshot_overlay_commander_ref_is_primary_and_reads_child_bytes()
    {
        string? baseDir = BasePackDirectory();
        string modDir = Path.Combine(ModsRoot(), "ex-commander-skills");
        if (baseDir == null || !File.Exists(Path.Combine(modDir, "assets", "tables", "def_commander.xml")))
        {
            return;
        }

        var primary = new DirectoryPackSource(modDir);
        var parent = new DirectoryPackSource(baseDir);
        var resolver = new ContentResolver(ContentKindRegistry.CreateDefault(), Engine);
        ResolvedContent snapshot = resolver.Resolve(primary, parent);
        IModSnapshot view = snapshot;

        ContentRef? commanderRef = view.ListContentRefs("assets/tables/")
            .FirstOrDefault(r => r.LogicalPath.EndsWith("def_commander.xml", StringComparison.Ordinal));
        Assert.NotNull(commanderRef);
        Assert.Equal(ModPackLayer.Primary, commanderRef!.Layer);

        Assert.True(view.TryReadRuntimeAsset("original/def_commander.xml", out byte[] bytes));
        string text = System.Text.Encoding.UTF8.GetString(bytes);
        Assert.Contains("skill2=\"99\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Snapshot_revision_alias_equals_fingerprint()
    {
        string? baseDir = BasePackDirectory();
        if (baseDir == null)
        {
            return;
        }

        var resolver = new ContentResolver(ContentKindRegistry.CreateDefault(), Engine);
        ResolvedContent snapshot = resolver.Resolve(new DirectoryPackSource(baseDir), null);
        Assert.Equal(snapshot.Fingerprint, ((IModSnapshot)snapshot).Revision);
    }

    [Fact]
    public void Loader_rejects_invalid_pack_when_schema_errors()
    {
        string temp = Path.Combine(Path.GetTempPath(), "ew4-mod-snapshot-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            File.WriteAllText(
                Path.Combine(temp, PackManifestPaths.FileName),
                """
                {
                  "schema": 1,
                  "id": "bad-empty",
                  "name": "bad",
                  "version": "0",
                  "requires": { "engine": ">=0.3.0" },
                  "formats": {
                    "scenario": 1,
                    "rules": 1,
                    "map": 1,
                    "commander": 1,
                    "unit": 1,
                    "country": 1,
                    "text": 1,
                    "script": 1,
                    "asset": 1,
                    "source": 1,
                    "extension": 1
                  }
                }
                """);
            File.WriteAllText(Path.Combine(temp, "not-allowed.txt"), "x");

            var primary = new DirectoryPackSource(temp);
            var resolver = new ContentResolver(ContentKindRegistry.CreateDefault(), Engine);
            var loader = new ModSnapshotLoader(resolver, new ContentValidator(ContentKindRegistry.CreateDefault()));

            Assert.Throws<ContentError>(() => loader.LoadActivatedView(primary, null));
        }
        finally
        {
            try
            {
                Directory.Delete(temp, recursive: true);
            }
            catch (IOException)
            {
            }
        }
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
