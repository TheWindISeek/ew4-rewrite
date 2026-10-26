using Ew4.Interface.Mod;
using Ew4.Platform.Mod.Snapshot;

namespace Ew4.Platform.Content;

public sealed partial record ResolvedContent
{
    public string PackId => Manifest.Id;

    public string Revision => Fingerprint;

    public bool ModFileExists(string modRelativePath) =>
        Source.Exists(ModPath.Normalize(modRelativePath));

    public byte[] ReadModFile(string modRelativePath) =>
        Source.Read(ModPath.Normalize(modRelativePath));

    public bool TryReadRuntimeAsset(string runtimePath, out byte[] bytes)
    {
        if (!ModAssetReader.TryResolve(Source, runtimePath, out string packPath))
        {
            bytes = Array.Empty<byte>();
            return false;
        }

        bytes = Source.Read(packPath);
        return true;
    }

    public IReadOnlyList<ModContentRef> ListContentRefs(string prefix = "")
    {
        IReadOnlyList<ModContentRef> all = ContentRefs ?? ModSnapshotIndex.Build(Source);
        if (string.IsNullOrEmpty(prefix))
        {
            return all;
        }

        string normalized = ModPath.Normalize(prefix);
        return all
            .Where(r => r.LogicalPath.StartsWith(normalized, StringComparison.Ordinal))
            .ToArray();
    }
}
