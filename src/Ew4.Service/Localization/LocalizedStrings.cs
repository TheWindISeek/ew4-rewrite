using System.Text;
using System.Text.Json;
using Ew4.Interface.Common;

namespace Ew4.Service.Localization;

/// <summary>
/// 内容包里的语言覆盖表（<c>client/lang/&lt;code&gt;.json</c>：<c>{ "schema": 1, "strings": { key: text } }</c>）。
/// 回退链：当前语言 → 默认语言 → 原版字符串表 → key。
/// </summary>
public sealed class LocalizedStrings
{
    private readonly Dictionary<string, IReadOnlyDictionary<string, string>> _overlays;

    private LocalizedStrings(Dictionary<string, IReadOnlyDictionary<string, string>> overlays) => _overlays = overlays;

    public static LocalizedStrings Empty { get; } = new([]);

    public static LocalizedStrings Load(Func<string, byte[]?> tryRead)
    {
        var overlays = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.Ordinal);
        foreach (LanguageInfo language in Languages.All)
        {
            byte[]? bytes = tryRead(Languages.OverlayPath(language.Code));
            if (bytes != null)
            {
                overlays[language.Code] = Parse(language.Code, Encoding.UTF8.GetString(bytes));
            }
        }

        return new LocalizedStrings(overlays);
    }

    /// <summary>语言列表：默认语言始终可用；其它语言需要内容包里有覆盖文件。</summary>
    public IReadOnlyList<LanguageInfo> Available() =>
        Languages.All.Where(l => l.Code == Languages.Default || _overlays.ContainsKey(l.Code)).ToArray();

    /// <summary>仅覆盖表的合并结果（默认语言覆盖 + 当前语言覆盖），给前端用。</summary>
    public IReadOnlyDictionary<string, string> Overlay(string language)
    {
        var merged = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string code in new[] { Languages.Default, language }.Distinct())
        {
            if (_overlays.TryGetValue(code, out IReadOnlyDictionary<string, string>? table))
            {
                foreach (var (key, text) in table)
                {
                    merged[key] = text;
                }
            }
        }

        return merged;
    }

    /// <summary>完整字符串表：原版表为底，覆盖表压在上面。</summary>
    public IReadOnlyDictionary<string, string> Resolve(string language, IReadOnlyDictionary<string, string> legacy)
    {
        var merged = new Dictionary<string, string>(legacy, StringComparer.Ordinal);
        foreach (var (key, text) in Overlay(language))
        {
            merged[key] = text;
        }

        return merged;
    }

    public static IReadOnlyDictionary<string, string> Parse(string code, string json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("strings", out JsonElement strings)
                || strings.ValueKind != JsonValueKind.Object)
            {
                throw new OperationException("language.table", $"{code}.json must contain a \"strings\" object.");
            }

            var table = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (JsonProperty entry in strings.EnumerateObject())
            {
                if (entry.Value.ValueKind != JsonValueKind.String)
                {
                    throw new OperationException("language.table", $"{code}.json: \"{entry.Name}\" must be a string.");
                }

                table[entry.Name] = entry.Value.GetString()!;
            }

            return table;
        }
        catch (JsonException error)
        {
            throw new OperationException("language.table", $"{code}.json is not valid JSON: {error.Message}");
        }
    }
}
