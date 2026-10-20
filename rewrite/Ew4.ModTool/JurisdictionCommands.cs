using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ew4.Core;
using Ew4.Legacy;
using Ew4.Mods;

namespace Ew4.ModTool;

internal static class JurisdictionCommands
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    { WriteIndented = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 8 };

    internal static object Run(string[] args, ModBase baseline, string? contentDirectory)
    {
        int expected = args[0] == "jurisdiction-apply" ? 4 : 3;
        if (args.Length != expected) throw new ArgumentException("用法: jurisdiction-export <源.btl> <输出.json> | jurisdiction-apply <源.btl> <配置.json> <输出.btl> | jurisdiction-strip <源.btl> <输出.btl>");
        string input = Path.GetFullPath(args[1]), output = Path.GetFullPath(args[^1]);
        if (args.Skip(1).SkipLast(1).Any(p => string.Equals(Path.GetFullPath(p), output, StringComparison.OrdinalIgnoreCase)) || File.Exists(output))
            throw new IOException("请指定尚不存在的新输出文件，工具不会覆盖源文件或既有成果");
        if (new FileInfo(input).Length > 256L * 1024 * 1024) throw new IOException("BTL超过256MiB预算");
        byte[] bytes = File.ReadAllBytes(input);
        var battle = LegacyBattle.Read(bytes);
        byte[]? binaryOutput = null;
        LegacyJurisdictionData? document = null;
        if (args[0] == "jurisdiction-strip") binaryOutput = battle.WithJurisdictions(null);
        else
        {
            string id = Path.GetFileNameWithoutExtension(input);
            byte[] Read(string path)
            {
                if (path == "original/" + id + ".btl") return bytes;
                string? candidate = contentDirectory == null ? null : Path.Combine(Path.GetFullPath(contentDirectory), path);
                return candidate != null && File.Exists(candidate) ? File.ReadAllBytes(candidate) : baseline.Read(path);
            }
            var content = baseline.Content(Read, [id]);
            var choice = content.Scenarios.Single();
            if (choice.Mode is not (1 or 2)) throw new RuleViolation("client.jurisdiction_mode", "手工辖区仅支持战役与征服；请保留剧本原文件名");
            string? country = choice.Mode == 2 ? battle.Factions.First(f => f.Camp != 4).CountryId : null;
            WorldState Start() => content.Start(id, country).Session.World;
            if (args[0] == "jurisdiction-export")
            {
                var world = Start();
                var cells = new List<int>();
                for (int y = 0; y < battle.Height; y++)
                    for (int x = 0; x < battle.Width; x++) cells.Add(world.Jurisdictions!.CellCenters[(battle.Top + y) * world.Width + battle.Left + x]);
                document = new(1, battle.MapId, world.Width, world.Height, battle.Left, battle.Top, battle.Width, battle.Height,
                    Convert.ToHexStringLower(SHA256.HashData(battle.ToLegacyBytes())), cells);
            }
            else if (args[0] == "jurisdiction-apply")
            {
                if (new FileInfo(args[2]).Length > 256L * 1024 * 1024) throw new IOException("辖区JSON超过256MiB预算");
                using var parsed = JsonDocument.Parse(File.ReadAllBytes(args[2]), new() { MaxDepth = 8 });
                if (parsed.RootElement.ValueKind != JsonValueKind.Object || parsed.RootElement.EnumerateObject().Select(p => p.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != parsed.RootElement.EnumerateObject().Count())
                    throw new IOException("辖区JSON必须是对象，且不得包含重复字段");
                document = parsed.RootElement.Deserialize<LegacyJurisdictionData>(Json) ?? throw new IOException("辖区JSON为空");
                binaryOutput = battle.WithJurisdictions(document);bytes = binaryOutput;
                // Run the actual startup path, including terrain/facility validation, before writing.
                Start();
            }
            else throw new ArgumentException("未知辖区命令");
        }
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        using (var stream = new FileStream(output, FileMode.CreateNew, FileAccess.Write))
        {
            if (binaryOutput != null) stream.Write(binaryOutput);
            else JsonSerializer.Serialize(stream, document, Json);
        }
        return new { output, cells = document?.CellCenters.Count, source = args[0] == "jurisdiction-export" ? battle.Jurisdictions == null ? "automatic" : "btl" : null };
    }
}
