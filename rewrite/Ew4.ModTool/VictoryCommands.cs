using System.Text.Json;
using Ew4.Core;
using Ew4.Legacy;
using Ew4.Mods;

namespace Ew4.ModTool;

internal static class VictoryCommands
{
    internal static object Run(string[] args, ModBase baseline, string? contentDirectory)
    {
        if (args.Length != (args[0] == "victory-apply" ? 4 : 3)) throw new ArgumentException("victory-export <source.btl> <new.json> | victory-apply <source.btl> <rules.json> <new.btl> | victory-strip <source.btl> <new.btl>");
        string input = Path.GetFullPath(args[1]), output = Path.GetFullPath(args[^1]);
        if (File.Exists(output) || args.Skip(1).SkipLast(1).Any(p => string.Equals(Path.GetFullPath(p), output, StringComparison.OrdinalIgnoreCase)))
            throw new IOException("Output must be a new file");
        var battle = LegacyBattle.Read(File.ReadAllBytes(input));
        byte[] bytes;
        if (args[0] == "victory-export") bytes = JsonSerializer.SerializeToUtf8Bytes(battle.Victory ?? new ExtensionVictoryRules(),
            new JsonSerializerOptions(ExtensionLoader.Json) { WriteIndented = true });
        else
        {
            var rules = args[0] == "victory-strip" ? null : JsonSerializer.Deserialize<ExtensionVictoryRules>(File.ReadAllBytes(args[2]), ExtensionLoader.Json)
                ?? throw new RuleViolation("extension.victory", "Missing victory rules");
            bytes = battle.WithVictory(rules);
            string id = Path.GetFileNameWithoutExtension(input);
            byte[] Read(string path)
            {
                if (path == "original/" + id + ".btl") return bytes;
                string? candidate = contentDirectory == null ? null : Path.Combine(Path.GetFullPath(contentDirectory), path);
                return candidate != null && File.Exists(candidate) ? File.ReadAllBytes(candidate) : baseline.Read(path);
            }
            var content = baseline.Content(Read, [id]);
            content.Start(id, content.Scenarios.Single().Mode == 2 ? battle.Factions.First(f => f.Camp != 4).CountryId : null);
        }
        Directory.CreateDirectory(Path.GetDirectoryName(output)!); File.WriteAllBytes(output, bytes);
        return new { output, bytes = bytes.Length };
    }
}
