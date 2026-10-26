using System;
using Ew4.Platform.Engine;
using Ew4.Platform.Mod;

namespace Ew4.Platform.Content;

public sealed class ContentResolver : IContentResolver
{
    private readonly IContentKindRegistry _registry;
    private readonly IEngine _engine;

    public ContentResolver(IContentKindRegistry registry, IEngine engine)
    {
        _registry = registry;
        _engine = engine;
    }

    public ResolvedContent Resolve(IModSource pack, IModSource? parent)
    {
        if (parent != null && pack.Manifest.Parent != null
            && !string.Equals(pack.Manifest.Parent, parent.Manifest.Id, StringComparison.Ordinal))
        {
            throw new ContentError(
                "mod.parent",
                $"Pack parent '{pack.Manifest.Parent}' does not match supplied parent '{parent.Manifest.Id}'.");
        }

        EngineRequirement.AssertCompatible(pack.Manifest.Requires, _engine.Info);
        IModSource effective = ModMerge.Apply(pack, parent);
        string fingerprint = ContentFingerprint.Compute(effective, _registry);
        return new ResolvedContent(pack.Manifest, fingerprint, effective);
    }
}
