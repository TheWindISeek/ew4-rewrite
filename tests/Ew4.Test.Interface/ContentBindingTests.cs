using Ew4.Interface.Runtime;
using Xunit;

namespace Ew4.Test.Interface;

public sealed class ContentBindingTests
{
    [Fact]
    public void Installed_mod_binding_has_no_workspace()
    {
        var binding = new ContentBinding("abc", ContentBindingKind.InstalledMod, "great-front");
        Assert.Null(binding.WorkspaceId);
        Assert.Throws<InvalidOperationException>(() => binding.EnsureWorkspace());
    }

    [Fact]
    public void Workspace_preview_requires_workspace_id()
    {
        var binding = new ContentBinding("abc", ContentBindingKind.WorkspacePreview, "base", "my-ws");
        binding.EnsureWorkspace();
        var bad = new ContentBinding("abc", ContentBindingKind.WorkspacePreview, "base", null);
        Assert.Throws<InvalidOperationException>(() => bad.EnsureWorkspace());
    }
}
