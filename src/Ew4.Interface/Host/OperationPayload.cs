using Ew4.Interface.Editor;
namespace Ew4.Interface.Host;

/// <summary>与 <see cref="Operations.OperationDescriptor"/> 配套的载荷；Backend 从 HTTP body/query 填充。</summary>
public abstract record OperationPayload;

/// <summary>Play 通道：<c>POST /api/play/{route}</c> 的 JSON body（或空）。</summary>
public sealed record PlayBodyPayload(string Body) : OperationPayload;

/// <summary>Platform：<c>/api/mods</c> 可选 JSON body。</summary>
public sealed record PlatformJsonPayload(string? Json) : OperationPayload;

/// <summary>Platform：<c>/api/mod-prepare</c> 上传文件的暂存路径与原始文件名。</summary>
public sealed record PlatformFilePayload(string SourcePath, string FileName) : OperationPayload;

/// <summary>Platform：<c>/api/mod-import</c> 表单字段。</summary>
public sealed record PlatformFormCommitPayload(string Token, string Id, string Name) : OperationPayload;

/// <summary>Editor 通道：workspace 相关操作的通用载荷（路径、字节、查询参数等）。</summary>
public sealed record EditorWorkspacePayload(
    string? WorkspaceId = null,
    string? RelativePath = null,
    byte[]? Body = null,
    string? ValidationLevel = null,
    string? FileName = null,
    string? CampaignId = null,
    bool Smoke = false,
    string? WorkspaceName = null,
    string? FromModId = null,
    int? LegacyId = null,
    bool Standalone = false,
    string? Mode = null,
    int? MapId = null,
    MapCropQuery? Crop = null) : OperationPayload;

/// <summary>Static 通道：路径已在 <see cref="Operations.StaticHttpAssetDescriptor"/> 中。</summary>
public sealed record StaticAssetPayload() : OperationPayload;
