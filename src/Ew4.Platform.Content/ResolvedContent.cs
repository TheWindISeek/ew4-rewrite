using System.IO;

namespace Ew4.Platform.Content;

/// <summary>
/// 校验通过后的不可变内容快照。
/// </summary>
public sealed partial record ResolvedContent(
    PackManifest Manifest,
    string Fingerprint,
    IPackSource Source,
    IReadOnlyList<ContentRef>? ContentRefs = null) : IModSnapshot;

public interface IContentResolver
{
    ResolvedContent Resolve(IPackSource pack, IPackSource? parent);
}

public interface IPackExporter
{
    void Export(IPackSource pack, Stream destination);
}
