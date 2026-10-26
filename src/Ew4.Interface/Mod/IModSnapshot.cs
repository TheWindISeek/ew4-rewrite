namespace Ew4.Interface.Mod;

/// <summary>激活载入后的只读 Mod 视图；实现通常为 <c>Ew4.Platform.Content.ResolvedContent</c>。</summary>
public interface IModSnapshot
{
    string PackId { get; }

    string Revision { get; }

    bool ModFileExists(string modRelativePath);

    byte[] ReadModFile(string modRelativePath);

    bool TryReadRuntimeAsset(string runtimePath, out byte[] bytes);

    IReadOnlyList<ModContentRef> ListContentRefs(string prefix = "");
}
