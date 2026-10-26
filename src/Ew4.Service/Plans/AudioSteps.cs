using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Ew4.Interface.Common;
using Ew4.Interface.Host;
using Ew4.Interface.Plans;
using Ew4.Service.Audio;
using Ew4.Service.Host;
using Ew4.Service.Play;

namespace Ew4.Service.Plans;

/// <summary>
/// 音频模块的 Step：开局/读档后选背景音乐，并把 <c>audio</c> 字段并入游玩响应。
/// 选曲是可选 Step：失败或没有曲目表时只记 diagnostics，不影响开局。
/// 战斗音效不在这里——它由前端按战斗时间轴（effects.json 的 sound 字段）与动画同步播放。
/// </summary>
public static class AudioSteps
{
    public static readonly PlanKey<BgmChoice> Bgm = new("audio.bgm");

    /// <summary>读开局响应里的玩家国家，查曲目表；找不到曲目就不写 <see cref="Bgm"/>。</summary>
    public static IStep PickBgm(IRuntimeHost host, PlayHost play) =>
        Steps.Of("audio.pick-bgm", [PlayPlans.Envelope], [Bgm], ctx =>
        {
            string? countryId = PlayerCountry(ctx.Get(PlayPlans.Envelope));
            if (host.ActiveSnapshot is not { } snapshot
                || !snapshot.TryReadRuntimeAsset(BgmCatalog.AssetPath, out byte[] table))
            {
                return;
            }

            BgmCatalog? user = play.SaveRoot is { } root ? new BgmPreferences(root).Load() : null;
            if (BgmCatalog.Parse(Encoding.UTF8.GetString(table)).Pick(countryId, user) is { } choice)
            {
                ctx.Set(Bgm, choice);
            }
        });

    /// <summary>music route 的实现：返回信封 JSON；可预期失败（坏参数、曲目不存在）返回 <c>ok:false</c>。</summary>
    public static string MusicConfig(IRuntimeHost host, PlayHost play, string body)
    {
        try
        {
            if (host.ActiveSnapshot is not { } snapshot || play.SaveRoot is not { } root)
            {
                throw new OperationException("audio.inactive", "No content pack is active.");
            }

            var prefs = new BgmPreferences(root);
            JsonObject request = string.IsNullOrWhiteSpace(body) ? [] : JsonNode.Parse(body) as JsonObject
                ?? throw new OperationException("audio.request", "Body must be an object.");
            string op = request["op"]?.GetValue<string>() ?? "get";
            BgmCatalog user;
            if (op == "set")
            {
                string? country = request["country"]?.GetValue<string>();
                string? track = request["track"]?.GetValue<string>();
                if (track != null && !snapshot.TryReadRuntimeAsset("client/" + BgmCatalog.Validate(track), out _))
                {
                    throw new OperationException("audio.track", "Track not found in active content: " + track);
                }

                user = prefs.Set(country, track);
            }
            else if (op == "get")
            {
                user = prefs.Load();
            }
            else
            {
                throw new OperationException("audio.request", "Unknown op: " + op);
            }

            BgmCatalog mod = snapshot.TryReadRuntimeAsset(BgmCatalog.AssetPath, out byte[] table)
                ? BgmCatalog.Parse(Encoding.UTF8.GetString(table))
                : BgmCatalog.Empty;
            var tracks = new JsonArray();
            foreach (var entry in snapshot.ListContentRefs("assets/client/music/").Select(r => r.LogicalPath).Distinct().Order(StringComparer.Ordinal))
            {
                string relative = entry["assets/client/".Length..];
                try { tracks.Add(BgmCatalog.Validate(relative)); } catch (OperationException) { }
            }

            return new JsonObject
            {
                ["ok"] = true,
                ["data"] = new JsonObject
                {
                    ["mod"] = JsonNode.Parse(mod.ToJson()),
                    ["user"] = JsonNode.Parse(user.ToJson()),
                    ["tracks"] = tracks,
                },
            }.ToJsonString();
        }
        catch (Exception error) when (error is OperationException or JsonException or InvalidOperationException or FormatException)
        {
            string code = error is OperationException op ? op.Code : "audio.request";
            return new JsonObject { ["ok"] = false, ["code"] = code, ["message"] = error.Message }.ToJsonString();
        }
    }

    /// <summary>把（可能存在的）选曲结果写进响应的 <c>data.audio</c>；失败的开局响应原样返回。</summary>
    public static IStep Compose() =>
        Steps.Of("play.compose-response", [PlayPlans.Envelope], [Bgm], [PlanKeys.Response], ctx =>
        {
            ctx.TryGet(Bgm, out BgmChoice? choice);
            ctx.Set(PlanKeys.Response, OperationResponse.Json(WithAudio(ctx.Get(PlayPlans.Envelope), choice)));
        });

    public static string WithAudio(string envelope, BgmChoice? choice)
    {
        JsonNode? root = JsonNode.Parse(envelope);
        if (root is JsonObject { } obj && obj["ok"]?.GetValue<bool>() == true && obj["data"] is JsonObject data)
        {
            data["audio"] = new JsonObject
            {
                ["bgm"] = choice == null ? null : new JsonObject { ["track"] = choice.Track, ["loop"] = choice.Loop },
            };
            return obj.ToJsonString();
        }

        return envelope;
    }

    public static string? PlayerCountry(string envelope)
    {
        using JsonDocument document = JsonDocument.Parse(envelope);
        if (!document.RootElement.TryGetProperty("data", out JsonElement data) || data.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        string? player = data.TryGetProperty("playerFaction", out JsonElement pf) && pf.ValueKind == JsonValueKind.String ? pf.GetString() : null;
        if (player != null && data.TryGetProperty("factions", out JsonElement factions) && factions.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement faction in factions.EnumerateArray())
            {
                if (faction.TryGetProperty("id", out JsonElement id) && id.GetString() == player
                    && faction.TryGetProperty("country", out JsonElement country) && country.ValueKind == JsonValueKind.String)
                {
                    return country.GetString();
                }
            }
        }

        return data.TryGetProperty("startCountry", out JsonElement start) && start.ValueKind == JsonValueKind.String ? start.GetString() : null;
    }
}
