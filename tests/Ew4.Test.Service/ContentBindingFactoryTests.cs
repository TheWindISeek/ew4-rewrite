using Ew4.Interface.Mod;
using Ew4.Interface.Runtime;
using Ew4.Platform.Content;
using Ew4.Platform.Engine;
using Ew4.Platform.Persistence.Packages;
using Ew4.Service.Host;
using Xunit;

namespace Ew4.Test.Service;

public sealed class ContentBindingFactoryTests
{
    [Fact]
    public void Returns_null_when_no_active_snapshot()
    {
        var host = new StubRuntimeHost { Snapshot = null };
        Assert.Null(ContentBindingFactory.From(host));
        Assert.Null(host.TryGetBinding());
    }

    [Fact]
    public void Installed_mod_binding_when_not_previewing()
    {
        var manifest = new ModManifest(1, "base", "Base", "1", null, new ModRequirements("1", []), new Dictionary<string, int>());
        var resolved = new ResolvedContent(manifest, "fp-test", new StubModSource(manifest));
        var host = new StubRuntimeHost
        {
            Snapshot = resolved,
            Preview = new HostPreviewState(false, null, null),
        };

        ContentBinding? binding = ContentBindingFactory.From(host);
        Assert.NotNull(binding);
        Assert.Equal(ContentBindingKind.InstalledMod, binding!.Kind);
        Assert.Equal("base", binding.ModId);
        Assert.Equal("fp-test", binding.Revision);
        Assert.Null(binding.WorkspaceId);
    }

    [Fact]
    public void Workspace_preview_binding_includes_workspace_id()
    {
        var manifest = new ModManifest(1, "overlay", "O", "1", "base", new ModRequirements("1", []), new Dictionary<string, int>());
        var resolved = new ResolvedContent(manifest, "rev-ws", new StubModSource(manifest));
        var host = new StubRuntimeHost
        {
            Snapshot = resolved,
            Preview = new HostPreviewState(true, "my-workspace", "base"),
        };

        ContentBinding? binding = ContentBindingFactory.From(host);
        Assert.NotNull(binding);
        Assert.Equal(ContentBindingKind.WorkspacePreview, binding!.Kind);
        Assert.Equal("my-workspace", binding.WorkspaceId);
    }

    private sealed class StubModSource : IModSource
    {
        public StubModSource(ModManifest manifest) => Manifest = manifest;

        public ModManifest Manifest { get; }

        public bool Exists(string path) => false;

        public byte[] Read(string path) => throw new NotSupportedException();

        public IEnumerable<string> List(string prefix) => Array.Empty<string>();
    }

    private sealed class StubRuntimeHost : IRuntimeHost, IContentBindingSource
    {
        public IModSnapshot? Snapshot { get; set; }

        public HostPreviewState Preview { get; set; } = new(false, null, null);

        public IModSnapshot? ActiveSnapshot => Snapshot;

        public HostPreviewState PreviewState => Preview;

        public ModStatus Status() => new([], null, false);

        public void Activate(string? modId) => throw new NotSupportedException();

        public void Remove(string modId) => throw new NotSupportedException();

        public void EnterWorkspacePreview(string workspaceRoot, string workspaceId) =>
            throw new NotSupportedException();

        public void RefreshWorkspacePreview(string workspaceRoot, string workspaceId) =>
            throw new NotSupportedException();

        public void ExitWorkspacePreview() => throw new NotSupportedException();

        public ContentBinding? TryGetBinding() => ContentBindingFactory.From(this);
    }
}
