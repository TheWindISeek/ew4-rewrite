using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Ew4.Platform.Content;

namespace Ew4.Tool.Import;

/// <summary>
/// 离线工具：为 APK 差异包补齐缺失国旗 HUD 图（<c>data/placeholders/flag_blank.png</c>）。
/// </summary>
public static class ModFlagPlaceholder
{
    public sealed record Result(int Added);

    public static Result Ensure(string modDirectory, string baseModDirectory, string? placeholderFlagPng = null)
    {
        string packRoot = Path.GetFullPath(modDirectory);
        string baseRoot = Path.GetFullPath(baseModDirectory);
        byte[] blank = File.ReadAllBytes(ResolvePlaceholderPath(placeholderFlagPng));
        IModSource overlay = new DirectoryModSource(packRoot);
        IModSource baseline = new DirectoryModSource(baseRoot);
        IReadOnlyDictionary<string, string> flags = ReadCountryFlags(overlay, baseline);
        int added = 0;
        foreach (string flag in flags.Values)
        {
            if (string.IsNullOrWhiteSpace(flag))
            {
                continue;
            }

            string logical = "assets/client/ui/image_flag_hd/" + flag + "1.png";
            if (overlay.Exists(logical) || baseline.Exists(logical))
            {
                continue;
            }

            string physical = Path.Combine(packRoot, logical.Replace('/', Path.DirectorySeparatorChar));
            string? parent = Path.GetDirectoryName(physical);
            if (!string.IsNullOrEmpty(parent))
            {
                Directory.CreateDirectory(parent);
            }

            File.WriteAllBytes(physical, blank);
            added++;
        }

        return new(added);
    }

    private static string ResolvePlaceholderPath(string? overridePath)
    {
        if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
        {
            return Path.GetFullPath(overridePath);
        }

        if (File.Exists("/src/Ew4.sln"))
        {
            return "/src/data/placeholders/flag_blank.png";
        }

        string repo = FindRepoRoot() ?? Directory.GetCurrentDirectory();
        string candidate = Path.Combine(repo, "data", "placeholders", "flag_blank.png");
        if (File.Exists(candidate))
        {
            return candidate;
        }

        throw new ContentError("placeholder.missing", "Missing data/placeholders/flag_blank.png");
    }

    private static string? FindRepoRoot()
    {
        string? dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && dir != null; i++)
        {
            if (File.Exists(Path.Combine(dir, "Ew4.sln")))
            {
                return dir;
            }

            dir = Path.GetDirectoryName(dir);
        }

        return null;
    }

    private static IReadOnlyDictionary<string, string> ReadCountryFlags(IModSource overlay, IModSource baseline)
    {
        const string path = "assets/client/countries.json";
        byte[] json = overlay.Exists(path) ? overlay.Read(path) : baseline.Read(path);
        using JsonDocument document = JsonDocument.Parse(json);
        var flags = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!document.RootElement.TryGetProperty("countries", out JsonElement countries))
        {
            return flags;
        }

        foreach (JsonProperty property in countries.EnumerateObject())
        {
            if (property.Value.TryGetProperty("flag", out JsonElement flagEl)
                && flagEl.ValueKind == JsonValueKind.String)
            {
                string? flag = flagEl.GetString();
                if (!string.IsNullOrWhiteSpace(flag))
                {
                    flags[property.Name] = flag;
                }
            }
        }

        return flags;
    }
}
