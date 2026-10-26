using System.Text.Json;
using System.Text.Json.Nodes;
using Ew4.Interface.Common;
using Ew4.Interface.Host;
using Ew4.Service.Host;
using Ew4.Service.Localization;
using Ew4.Service.Play;

namespace Ew4.Service.Plans;

/// <summary>语言设置：读当前语言与可用语言，或切换语言（会重建游玩内容并清掉局内会话）。</summary>
public static class LanguageSteps
{
    public static string Config(IRuntimeHost host, PlayHost play, string body)
    {
        try
        {
            if (host.ActiveSnapshot is not { } snapshot || play.StateRoot is not { } stateRoot)
            {
                throw new OperationException("language.inactive", "No content pack is active.");
            }

            JsonObject request = string.IsNullOrWhiteSpace(body) ? [] : JsonNode.Parse(body) as JsonObject
                ?? throw new OperationException("language.request", "Body must be an object.");
            string op = request["op"]?.GetValue<string>() ?? "get";
            var settings = new UserSettings(stateRoot);
            if (op == "set")
            {
                string? language = request["language"]?.GetValue<string>();
                if (!Languages.IsKnown(language))
                {
                    throw new OperationException("language.unknown", "Unknown language: " + language);
                }

                if (!string.Equals(settings.Language, language, StringComparison.Ordinal))
                {
                    play.SetLanguage(language!);
                }
            }
            else if (op != "get")
            {
                throw new OperationException("language.request", "Unknown op: " + op);
            }

            LocalizedStrings localized = LocalizedStrings.Load(path => snapshot.TryReadRuntimeAsset(path, out byte[] bytes) ? bytes : null);
            string current = settings.Language;
            if (localized.Available().All(l => l.Code != current))
            {
                current = Languages.Default;
            }

            var strings = new JsonObject();
            foreach (var (key, text) in localized.Overlay(current))
            {
                strings[key] = text;
            }

            var available = new JsonArray();
            foreach (LanguageInfo language in localized.Available())
            {
                available.Add(new JsonObject { ["code"] = language.Code, ["name"] = language.Name });
            }

            return new JsonObject
            {
                ["ok"] = true,
                ["data"] = new JsonObject { ["language"] = current, ["available"] = available, ["strings"] = strings },
            }.ToJsonString();
        }
        catch (Exception error) when (error is OperationException or JsonException or InvalidOperationException or FormatException)
        {
            string code = error is OperationException op ? op.Code : "language.request";
            return new JsonObject { ["ok"] = false, ["code"] = code, ["message"] = error.Message }.ToJsonString();
        }
    }

    /// <summary>APK 导出的前端接口占位：契约见 docs/task/plan/apk-export.md §14，实现由 Ew4.Tool 完成。</summary>
    public static string ExportApkUnimplemented() =>
        new JsonObject
        {
            ["ok"] = false,
            ["code"] = "export.unimplemented",
            ["message"] = "APK export is not implemented yet.",
        }.ToJsonString();
}
