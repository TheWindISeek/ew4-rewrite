using Ew4.Interface.Common;

namespace Ew4.Interface.Editor;

/// <summary>
/// Editor 通道中 <b>Content 执行平面</b> 的 workspace 操作（校验、导出、overlay 文件等）。
/// 由 Service 的 EditorPlans 调用，不经 Engine。
/// </summary>
public sealed record MapCropQuery(int X, int Y, int W, int H);

public interface IEditorWorkspaceOperations
{
    string ListWorkspacesJson();

    string CreateWorkspaceJson(string id, string name, string? fromModId, string? mode = null);

    /// <summary>该 workspace 的编辑模式与能力集。</summary>
    string CapabilitiesJson(string workspaceId);

    /// <summary>base 里可选的地图（id、名称、尺寸、hash）。</summary>
    string MapListJson(string workspaceId);

    string MapDescribeJson(string workspaceId, int mapId);

    /// <summary>地图预览；crop 省略为整图。</summary>
    string MapPreviewJson(string workspaceId, int mapId, MapCropQuery? crop);

    void DeleteWorkspace(string workspaceId);

    string ValidateJson(string workspaceId, ValidationLevel level);

    string ExportJson(string workspaceId, ValidationLevel level, string? fileName, bool standalone = false);

    string ListCampaignsJson(string workspaceId);

    byte[] ReadFile(string workspaceId, string relativePath);

    string WriteFileJson(string workspaceId, string relativePath, byte[] content);
}
