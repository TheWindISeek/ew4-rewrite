namespace Ew4.Platform.Content;

/// <summary>
/// 从 Pack 内 <c>scenarios/*.json</c> 读取选关列表显示名（差异 Mod 覆盖 <c>name</c> 时优先于 battlelist）。
/// </summary>
public static class ScenarioCatalogNames
{
    public static IReadOnlyDictionary<string, string> ReadDisplayNames(IPackSource source) =>
        PackScenarioCatalog.ReadOverlayDisplayNames(source);
}
