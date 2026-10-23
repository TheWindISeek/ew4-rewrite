namespace Ew4.Platform.Content;

public sealed partial record ResolvedContent
{
    string IModSnapshot.Revision => Fingerprint;

    public bool PackFileExists(string packRelativePath) =>
        Source.Exists(PackPath.Normalize(packRelativePath));

    public byte[] ReadPackFile(string packRelativePath) =>
        Source.Read(PackPath.Normalize(packRelativePath));

    public bool TryReadRuntimeAsset(string runtimePath, out byte[] bytes)
    {
        if (!PackAssetReader.TryResolve(Source, runtimePath, out string packPath))
        {
            bytes = Array.Empty<byte>();
            return false;
        }

        bytes = Source.Read(packPath);
        return true;
    }

    public IReadOnlyList<ContentRef> ListContentRefs(string prefix = "") =>
        ModSnapshotIndex.Build(Source)
            .Where(r => string.IsNullOrEmpty(prefix)
                || r.LogicalPath.StartsWith(PackPath.Normalize(prefix), StringComparison.Ordinal))
            .ToArray();
}
