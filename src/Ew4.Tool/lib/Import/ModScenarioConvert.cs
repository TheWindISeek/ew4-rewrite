using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Ew4.Platform.Mod.Scenario;
using Ew4.Platform.Persistence.Packages;

namespace Ew4.Tool.Lib.Import;

/// <summary>
/// 离线工具：把 mod 的 <c>scenarios/*.json</c>（schema 1 的 legacy btl 索引桩）转成 schema 3 场景 JSON，
/// 逐份校验往返字节一致；已是 schema 3 的文件按当前转换器重新生成（升级）。
/// </summary>
public static class ModScenarioConvert
{
    public sealed record Result(int Converted, int Skipped);

    public static Result ToSchema3(string modDirectory)
    {
        string mod = Path.GetFullPath(modDirectory);
        var converter = new ScenarioBtlConverter();
        int converted = 0, skipped = 0;
        foreach (string file in Directory.GetFiles(Path.Combine(mod, "scenarios"), "*.json").OrderBy(f => f, StringComparer.Ordinal))
        {
            if (file.EndsWith(".ew4scenario.json", StringComparison.OrdinalIgnoreCase)) { skipped++; continue; }
            byte[] text = File.ReadAllBytes(file);
            using var root = JsonDocument.Parse(text);
            JsonElement element = root.RootElement;
            bool isSchema3 = element.TryGetProperty("schema", out var schema) && schema.ValueKind == JsonValueKind.Number
                && schema.GetInt32() == ScenarioBtlConverter.SchemaVersion;
            string id = element.TryGetProperty("id", out var idElement) ? idElement.GetString() ?? "" : "";
            string? name = element.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String
                ? nameElement.GetString() : null;
            byte[]? btl = null;
            if (isSchema3)
            {
                btl = converter.ToBtl(element, new()).Bytes;
            }
            else if (element.TryGetProperty("legacy", out var legacy) && legacy.TryGetProperty("btl", out var btlPath))
            {
                string relative = btlPath.GetString() ?? "";
                string fileName = Path.GetFileName(relative);
                string baseDirectory = Path.Combine(Path.GetDirectoryName(mod) ?? "", "base");
                string? path = new[]
                {
                    Path.Combine(mod, relative), Path.Combine(baseDirectory, relative),
                    Path.Combine(mod, "assets", "original", fileName), Path.Combine(baseDirectory, "assets", "original", fileName),
                }.FirstOrDefault(File.Exists);
                if (path == null) { Console.Error.WriteLine("skip " + id + ": btl not found (" + relative + ")"); skipped++; continue; }
                btl = File.ReadAllBytes(path);
            }

            if (btl == null || id.Length == 0) { skipped++; continue; }
            string json = converter.ToJson(id, btl, new(name)).Json!;
            using var check = JsonDocument.Parse(json);
            if (!converter.ToBtl(check.RootElement, new()).Bytes.AsSpan().SequenceEqual(btl))
                throw new InvalidOperationException("roundtrip mismatch: " + id);
            File.WriteAllText(file, json);
            converted++;
        }

        if (converted > 0) ModManifestProfile.WriteEmbeddedJsonProfile(mod);
        return new(converted, skipped);
    }
}
