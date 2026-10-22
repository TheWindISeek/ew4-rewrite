using System.Security.Cryptography;
using System.Text.Json;
using Ew4.Legacy;
using Ew4.Mods;

namespace Ew4.ModTool;

internal static class CountryCommands
{
    internal static object Run(string[] args, ModBase baseline, string? contentDirectory)
    {
        int expected = args[0] == "country-apply" ? 4 : 3;
        if (args.Length != expected) throw new ArgumentException("country-export <source.btl> <output.json> | country-apply <source.btl> <config.json> <output.btl> | country-strip <source.btl> <output.btl>");
        string input = Path.GetFullPath(args[1]), output = Path.GetFullPath(args[^1]);
        if (args.Skip(1).SkipLast(1).Any(p => string.Equals(Path.GetFullPath(p), output, StringComparison.OrdinalIgnoreCase)) || File.Exists(output))
            throw new IOException("Specify a new output file; country commands do not overwrite existing files");
        if (new FileInfo(input).Length > 256L * 1024 * 1024) throw new IOException("BTL exceeds 256 MiB");
        byte[] bytes = File.ReadAllBytes(input);
        var battle = LegacyBattle.Read(bytes);
        byte[]? binary = null;
        var document = battle.CountryIds ?? new LegacyCountryIds(1, Convert.ToHexStringLower(SHA256.HashData(battle.ToLegacyBytes())),
            battle.Factions.Select(f => f.CountryId).ToArray());
        if (args[0] == "country-strip") binary = battle.WithCountryIds(null);
        else if (args[0] == "country-apply")
        {
            if (new FileInfo(args[2]).Length > 2 * 1024 * 1024) throw new IOException("CNTR JSON exceeds 2 MiB");
            document = ExtensionLoader.ParseJson<LegacyCountryIds>(File.ReadAllBytes(args[2]));
            binary = battle.WithCountryIds(document);
            battle = LegacyBattle.Read(binary);
            string id = Path.GetFileNameWithoutExtension(input);
            byte[] Read(string path)
            {
                if (path == "original/" + id + ".btl") return binary;
                string? candidate = contentDirectory == null ? null : Path.Combine(Path.GetFullPath(contentDirectory), path);
                return candidate != null && File.Exists(candidate) ? File.ReadAllBytes(candidate) : baseline.Read(path);
            }
            var content = baseline.Content(Read, [id]);
            var scenario = content.Scenarios.Single();
            if (scenario.Mode != 3) content.Start(id, scenario.Mode == 2 ? battle.Factions.First(f => f.Camp != 4).CountryId : null);
            else content.Battle(id);
        }
        else if (args[0] != "country-export") throw new ArgumentException("Unknown country command");
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        using (var stream = new FileStream(output, FileMode.CreateNew, FileAccess.Write))
        {
            if (binary != null) stream.Write(binary);
            else JsonSerializer.Serialize(stream, document, JurisdictionCommands.Json);
        }
        return new { output, factions = document.Factions.Count, embedded = args[0] != "country-strip" };
    }
}
