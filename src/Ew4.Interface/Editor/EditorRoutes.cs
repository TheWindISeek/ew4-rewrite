namespace Ew4.Interface.Editor;

/// <summary>创作 API 路由片段（相对 <c>/api/editor</c>）。</summary>
public static class EditorRoutes
{
    public const string Workspaces = "workspaces";
    public const string PreviewEnter = "preview/enter";
    public const string PreviewRefresh = "preview/refresh";
    public const string PreviewExit = "preview/exit";
    public const string Commander = "commander";
    public const string Campaign = "campaign";

    public static readonly string[] All =
    [
        Workspaces,
        PreviewEnter,
        PreviewRefresh,
        PreviewExit,
        Commander,
        Campaign,
    ];
}
