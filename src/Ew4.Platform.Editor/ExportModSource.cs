using System.Text.Json.Nodes;
using Ew4.Platform.Content;
using Ew4.Platform.Persistence.Workspaces;

namespace Ew4.Platform.Editor;

/// <summary>
/// 导出视图：文件列表在构造时定格，并排除 workspace 自己的 <c>exports/</c> 与元数据（否则正在写的归档会被打进自己）。
/// <paramref name="dropParent"/> 为真时清单去掉 parent，导出后无需 base 即可独立使用（配合合并后的内容视图）。
/// </summary>
public sealed class ExportModSource : IModSource
{
    private readonly IModSource _inner;
    private readonly bool _dropParent;
    private readonly string[] _paths;

    public ExportModSource(IModSource inner, bool dropParent)
    {
        _inner = inner;
        _dropParent = dropParent;
        _paths = inner.List("")
            .Where(p => !p.StartsWith("exports/", StringComparison.Ordinal) && p != WorkspaceStore.MetadataFileName)
            .ToArray();
        Manifest = dropParent ? inner.Manifest with { Parent = null } : inner.Manifest;
    }

    public ModManifest Manifest { get; }

    public bool Exists(string path) => _inner.Exists(path);

    public IEnumerable<string> List(string prefix) =>
        _paths.Where(p => string.IsNullOrEmpty(prefix) || p.StartsWith(prefix, StringComparison.Ordinal));

    public byte[] Read(string path)
    {
        byte[] bytes = _inner.Read(path);
        if (!_dropParent || !string.Equals(path, ModManifestPaths.FileName, StringComparison.Ordinal)) return bytes;
        JsonObject manifest = JsonNode.Parse(bytes)!.AsObject();
        manifest.Remove("parent");
        return System.Text.Encoding.UTF8.GetBytes(manifest.ToJsonString());
    }
}
