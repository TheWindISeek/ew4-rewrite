namespace Ew4.Platform.Content;

public static class PackScenarioIds
{
    /// <summary>
    /// 列出 Pack 内剧本 id（<c>scenarios/*.json</c> + 有 BTL 的 battlelist 条目），供 Play 与 <c>ClientContent</c> 使用。
    /// </summary>
    public static IReadOnlyList<string> List(ResolvedContent content) => PackScenarioCatalog.ListIds(content);
}
