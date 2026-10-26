using System.Text.Json;
using Ew4.Interface.Common;

namespace Ew4.Service.Audio;

/// <summary>一次选曲结果。<see cref="Track"/> 是相对 <c>/content/</c> 的路径（即 mod 的 <c>client/</c> 下）。</summary>
public sealed record BgmChoice(string Track, bool Loop = true);

/// <summary>
/// 背景音乐表（mod 的 <c>client/music.json</c>）：国家 id → 曲目，没有专属曲目时用 <c>default</c>。
/// <code>{ "schema": 1, "default": "music/a.ogg", "countries": { "fra": "music/b.ogg" } }</code>
/// </summary>
public sealed class BgmCatalog
{
    public const string AssetPath = "client/music.json";

    private static readonly string[] Extensions = [".ogg", ".mp3", ".m4a", ".wav"];

    private readonly string? _default;
    private readonly IReadOnlyDictionary<string, string> _countries;

    private BgmCatalog(string? defaultTrack, IReadOnlyDictionary<string, string> countries)
    {
        _default = defaultTrack;
        _countries = countries;
    }

    public static BgmCatalog Empty { get; } = new(null, new Dictionary<string, string>());

    public static BgmCatalog Parse(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new OperationException("audio.music_table", "music.json must be an object.");
        }

        string? defaultTrack = null;
        if (root.TryGetProperty("default", out JsonElement def) && def.ValueKind == JsonValueKind.String)
        {
            defaultTrack = Validate(def.GetString()!);
        }

        var countries = new Dictionary<string, string>(StringComparer.Ordinal);
        if (root.TryGetProperty("countries", out JsonElement map) && map.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty entry in map.EnumerateObject())
            {
                if (entry.Value.ValueKind != JsonValueKind.String)
                {
                    throw new OperationException("audio.music_table", $"countries.{entry.Name} must be a string.");
                }

                countries[entry.Name] = Validate(entry.Value.GetString()!);
            }
        }

        return new BgmCatalog(defaultTrack, countries);
    }

    public string? Default => _default;

    public IReadOnlyDictionary<string, string> Countries => _countries;

    /// <summary>
    /// 选曲：用户国家 → mod 国家 → 用户默认 → mod 默认。
    /// 用户表（<paramref name="user"/>）来自玩家本机配置，让玩家按势力覆盖 mod 作者的安排。
    /// </summary>
    public BgmChoice? Pick(string? countryId, BgmCatalog? user = null)
    {
        string? track = null;
        if (countryId != null)
        {
            if (user != null && user._countries.TryGetValue(countryId, out string? mine))
            {
                track = mine;
            }
            else if (_countries.TryGetValue(countryId, out string? own))
            {
                track = own;
            }
        }

        track ??= user?._default ?? _default;
        return track == null ? null : new BgmChoice(track);
    }

    public BgmCatalog With(string? countryId, string? track)
    {
        var countries = new Dictionary<string, string>(_countries, StringComparer.Ordinal);
        string? def = _default;
        if (countryId == null)
        {
            def = track == null ? null : Validate(track);
        }
        else if (track == null)
        {
            countries.Remove(countryId);
        }
        else
        {
            countries[countryId] = Validate(track);
        }

        return new BgmCatalog(def, countries);
    }

    public string ToJson()
    {
        var root = new System.Text.Json.Nodes.JsonObject { ["schema"] = 1 };
        if (_default != null)
        {
            root["default"] = _default;
        }

        var map = new System.Text.Json.Nodes.JsonObject();
        foreach (var (country, track) in _countries.OrderBy(e => e.Key, StringComparer.Ordinal))
        {
            map[country] = track;
        }

        root["countries"] = map;
        return root.ToJsonString();
    }

    public static string Validate(string track)
    {
        bool ok = track.Length is > 0 and < 200
            && !track.StartsWith('/')
            && !track.Contains("..", StringComparison.Ordinal)
            && !track.Contains('\\')
            && !track.Contains(':')
            && Extensions.Any(e => track.EndsWith(e, StringComparison.OrdinalIgnoreCase));
        return ok ? track : throw new OperationException("audio.track", "Invalid music track path: " + track);
    }
}
