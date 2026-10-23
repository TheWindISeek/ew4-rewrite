namespace Ew4.Platform.Content;

/// <summary>
/// 激活载入后的只读 Mod 视图（使用期入口）。见 <c>docs/arch/mod-view.md</c>。
/// </summary>
public interface IModSnapshot
{
    ModManifest Manifest { get; }

    /// <summary>内容修订（指纹），使用期对账用。</summary>
    string Revision { get; }

    /// <summary>合并后的底层包源（过渡；新代码优先用 <see cref="ReadPackFile"/>）。</summary>
    IModSource Source { get; }

    bool PackFileExists(string packRelativePath);

    byte[] ReadPackFile(string packRelativePath);

    bool TryReadRuntimeAsset(string runtimePath, out byte[] bytes);

    IReadOnlyList<ContentRef> ListContentRefs(string prefix = "");
}
