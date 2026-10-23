namespace Ew4.Platform.Content;

public sealed partial record ResolvedContent
{
    string IModSnapshot.Revision => Fingerprint;

    public bool PackFileExists(string packRelativePath) =>
        Source.Exists(ModPath.Normalize(packRelativePath));

    public byte[] ReadPackFile(string packRelativePath) =>
        Source.Read(ModPath.Normalize(packRelativePath));

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

    public IReadOnlyList<ContentRef> ListContentRefs(string prefix = "")
    {
        IReadOnlyList<ContentRef> all = ContentRefs ?? ModSnapshotIndex.Build(Source);
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
