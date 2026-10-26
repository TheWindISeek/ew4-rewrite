namespace Ew4.Platform.Editor.Campaign;

/// <summary>编辑模式：BaseTweak 只在 base 已有内容上增删改；Free 可自行安排剧本与地图。</summary>
public enum EditorMode
{
    BaseTweak,
    Free,
}

/// <summary>编辑会话允许做的事；会话按它校验每一次编辑。</summary>
public sealed record Capabilities(
    bool EditCatalog,
    bool EditScenarioUnits,
    bool EditMap,
    bool CreateScenario,
    bool EditEvents)
{
    public static Capabilities For(EditorMode mode) => mode switch
    {
        EditorMode.BaseTweak => new(EditCatalog: true, EditScenarioUnits: true, EditMap: false, CreateScenario: false, EditEvents: true),
        EditorMode.Free => new(true, true, true, true, true),
        _ => throw new ArgumentOutOfRangeException(nameof(mode)),
    };
}

/// <summary>EditorMode 与 workspace 元数据 / HTTP 里字符串的互转。</summary>
public static class EditorModes
{
    public static string Format(EditorMode mode) => mode == EditorMode.Free ? "free" : "baseTweak";

    public static EditorMode Parse(string? text) => string.IsNullOrWhiteSpace(text) ? EditorMode.BaseTweak
        : string.Equals(text, "free", StringComparison.OrdinalIgnoreCase) ? EditorMode.Free
        : string.Equals(text, "baseTweak", StringComparison.OrdinalIgnoreCase) ? EditorMode.BaseTweak
        : throw new Ew4.Platform.Content.ContentError("workspace.mode", "Unknown editor mode '" + text + "'; use 'baseTweak' or 'free'.");
}
