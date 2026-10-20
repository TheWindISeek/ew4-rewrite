using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Ew4.Client;
using Ew4.Legacy;
using Ew4.Mods;
using Ew4.Persistence;

internal static class JurisdictionModChecks
{
    internal static int Run(string root)
    {
        int checks = 0;
        void Check(bool ok, string message) { if (!ok) throw new Exception("JURI import: " + message);checks++; }
        string data = Path.Combine(root, "rewrite/data"), run = Path.Combine(root, "rewrite/reports/jurisdiction-extension-20261020/import-" + Guid.NewGuid().ToString("N"));
        var baseline = new ModBase(p => File.ReadAllBytes(p.StartsWith("frontend/") ? Path.Combine(root, "rewrite/Ew4.Web/wwwroot", p[9..]) : Path.Combine(data, p)),
            Directory.EnumerateFiles(data, "*", SearchOption.AllDirectories).Select(p => Path.GetRelativePath(data, p).Replace('\\', '/')));
        var content = baseline.Content(baseline.Read, ["conquest1"]);var btl = content.Battle("conquest1");
        var original = content.Start("conquest1", "fra").Session.World;
        var values = new List<int>();for (int y = 0; y < btl.Height; y++) for (int x = 0; x < btl.Width; x++) values.Add(original.Jurisdictions!.CellCenters[(y + btl.Top) * original.Width + btl.Left + x]);
        int cell = Enumerable.Range(0, original.Cells.Count).First(i => original.Cells[i].Terrain.Active && !original.Cells[i].Terrain.Sea && original.Cells[i].BuildingKind == null && original.Jurisdictions!.CellCenters[i] >= 0);
        int center = original.Jurisdictions!.CellCenters.First(c => c >= 0 && c != original.Jurisdictions.CellCenters[cell]);
        int index = (cell / original.Width - btl.Top) * btl.Width + cell % original.Width - btl.Left;values[index] = center;
        var doc = new LegacyJurisdictionData(1, btl.MapId, original.Width, original.Height, btl.Left, btl.Top, btl.Width, btl.Height,
            Convert.ToHexStringLower(SHA256.HashData(btl.ToLegacyBytes())), values);
        byte[] extended = btl.WithJurisdictions(doc);
        string raw = Path.Combine(run, "assets");Directory.CreateDirectory(raw);File.WriteAllBytes(Path.Combine(raw, "conquest1.btl"), extended);
        var store = new ModStore(baseline, Path.Combine(run, "mods"), Path.Combine(run, "saves"));
        var imported = store.Import(raw, "juri-test", "手工辖区测试");
        Check(store.Read("original/conquest1.btl").SequenceEqual(btl.ToBytes()), "import does not alter active builtin");
        store.Activate(imported.Key);
        Check(store.Read("original/conquest1.btl").SequenceEqual(extended), "raw BTL import preserves extension bytes");
        var modContent = baseline.Content(store.Read, ["conquest1"]);var session = modContent.Start("conquest1", "fra").Session;
        Check(session.World.Jurisdictions!.CellCenters[cell] == center, "converted content starts with author assignments");
        Check(imported.LogicRevision != new ModStore(baseline, Path.Combine(run, "plain-mods"), Path.Combine(run, "plain-saves")).Import(WritePlain(), "juri-test", "自动辖区测试").LogicRevision,
            "manual data participates in package logic identity");
        string WritePlain() { string plain = Path.Combine(run, "plain");Directory.CreateDirectory(plain);File.WriteAllBytes(Path.Combine(plain, "conquest1.btl"), btl.ToBytes());return plain; }
        var result = JsonDocument.Parse(store.Dispatch("/api/start", "{\"scenario\":\"conquest1\",\"country\":\"fra\"}"));
        Check(result.RootElement.GetProperty("ok").GetBoolean(), "actual ModStore game start");
        string save = Path.Combine(run, "saves/mods/juri-test", imported.LogicRevision, "saves/mode-2/slot-0.json");
        var slot = JsonSerializer.Deserialize<ClientSlot>(File.ReadAllBytes(save), ClientGame.Json)!;
        var restored = SessionSave.Decode(slot.Session, new(session.ContentRevision, session.World.ContentRevision, session.Account.ContentRevision));
        Check(restored.World.Jurisdictions!.CellCenters[cell] == center, "isolated mod autosave retains custom partition");
        string export = Path.Combine(run, "jurisdiction.ew4mod");store.Export(imported.Key, export);
        var other = new ModStore(baseline, Path.Combine(run, "roundtrip"), Path.Combine(run, "roundtrip-saves"));
        Check(other.Import(export, "ignored", "ignored").Key == imported.Key, "sealed offline package roundtrip");
        string zipPath = Path.Combine(run, "assets.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create)) { using var entry = zip.CreateEntry("assets/conquest1.btl").Open();entry.Write(extended); }
        Check(other.Import(zipPath, "juri-test", "手工辖区测试").Key == imported.Key, "assets ZIP uses same extension-compatible import path");
        var invalid = values.ToArray();invalid[index] = cell;
        File.WriteAllBytes(Path.Combine(raw, "conquest1.btl"), btl.WithJurisdictions(doc with { CellCenters = invalid }));
        bool rejected = false;try { store.Import(raw, "bad-juri", "错误辖区"); } catch (InvalidDataException e) { rejected = e.Message.Contains("conquest1") && e.Message.Contains(cell.ToString()); }
        Check(rejected, "bad manual center reports scenario and exact tile without automatic fallback");
        Check(store.List().Single(m => m.Active).Key == imported.Key, "failed import preserves current active content");
        store.Activate(null);Check(store.Read("original/conquest1.btl").SequenceEqual(btl.ToBytes()), "switch to builtin restores unextended BTL");
        File.WriteAllText(Path.Combine(run, "results.json"), JsonSerializer.Serialize(new { checks, errors = 0, imported.Key, changedCell = cell, manualCenter = center }));
        return checks;
    }
}
