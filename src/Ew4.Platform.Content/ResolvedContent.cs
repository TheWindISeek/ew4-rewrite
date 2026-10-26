using System.IO;
using Ew4.Interface.Mod;

namespace Ew4.Platform.Content;

/// <summary>
/// 校验通过后的不可变内容快照。
/// </summary>
public sealed partial record ResolvedContent(
    ModManifest Manifest,
    string Fingerprint,
    IModSource Source,
    IReadOnlyList<ModContentRef>? ContentRefs = null) : IModSnapshot;

public interface IContentResolver
{
    ResolvedContent Resolve(IModSource pack, IModSource? parent);
}

public interface IModExporter
{
    void Export(IModSource pack, Stream destination);
}
