namespace Ew4.Service.Localization;

public sealed record LanguageInfo(string Code, string Name);

/// <summary>受支持的界面语言。新增语言只需在这里加一行，再给内容包放 <c>client/lang/&lt;code&gt;.json</c>。</summary>
public static class Languages
{
    public const string Default = "zh-Hans";

    public static IReadOnlyList<LanguageInfo> All { get; } =
    [
        new("zh-Hans", "简体中文"),
        new("zh-Hant", "繁體中文"),
        new("en", "English"),
    ];

    public static bool IsKnown(string? code) =>
        code != null && All.Any(l => string.Equals(l.Code, code, StringComparison.Ordinal));

    /// <summary>未知或空 → 默认语言。</summary>
    public static string Normalize(string? code) => IsKnown(code) ? code! : Default;

    public static string OverlayPath(string code) => $"client/lang/{code}.json";
}
