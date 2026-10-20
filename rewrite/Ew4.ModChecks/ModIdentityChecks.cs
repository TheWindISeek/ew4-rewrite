using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Ew4.Mods;

internal static class ModIdentityChecks
{
    internal static int Run(string root)
    {
        string run = Path.Combine(root, "rewrite/reports/mod-identity-20261020", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(run); int checks = 0;
        void Check(bool value, string label) { if (!value) throw new Exception(label); checks++; }
        string Archive(string entry, byte[] bytes)
        {
            string path = Path.Combine(run, Guid.NewGuid().ToString("N") + ".zip");
            using var zip = ZipFile.Open(path, ZipArchiveMode.Create); using var stream = zip.CreateEntry(entry).Open(); stream.Write(bytes); return path;
        }
        string resource = Archive("assets/test.txt", [1]);
        var english = ModIdentityReader.Read(resource, "My Great Mod v2.zip");
        Check(english.Id == "my-great-mod-v2" && english.Name == "My Great Mod v2", "filename identity");
        var chinese = ModIdentityReader.Read(resource, "民国风云.apk");
        Check(chinese.Id.StartsWith("mod-") && chinese.Name == "民国风云", "Chinese filename");
        Check(ModIdentityReader.Read(resource, "民国风云.zip").Id == chinese.Id, "stable ID across extensions");
        Check(ModIdentityReader.Read(resource, "另一部风云.zip").Id != chinese.Id, "distinct Chinese IDs");
        foreach (string name in new[] { "builtin.zip", "CON.apk", "...apk", new string('a', 120) + ".zip" })
        { var identity = ModIdentityReader.Read(resource, name); ModFiles.Identifier(identity.Id); Check(identity.Id.Length <= 64, "reserved/long filename"); }
        string xml = Archive("AndroidManifest.xml", Encoding.UTF8.GetBytes("<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\" package=\"com.example.mod\"><application android:label=\"民国 Mod\"/></manifest>"));
        var apk = ModIdentityReader.Read(xml, "unhelpful.apk");
        Check(apk.Id == "com.example.mod" && apk.Name == "民国 Mod" && apk.Source == "apk", "APK literal metadata");
        string reference = Archive("AndroidManifest.xml", Encoding.UTF8.GetBytes("<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\" package=\"com.example.mod\"><application android:label=\"@string/app_name\"/></manifest>"));
        Check(ModIdentityReader.Read(reference, "有名的游戏.apk").Name == "有名的游戏", "resource label uses filename");
        foreach (bool utf8 in new[] { true, false })
        {
            var binary = ModIdentityReader.Read(Archive("AndroidManifest.xml", BinaryManifest(utf8)), "fallback.apk");
            Check(binary.Id == "com.example.binary" && binary.Name == "测试应用", "binary manifest " + utf8);
        }
        var malformed = ModIdentityReader.Read(Archive("AndroidManifest.xml", [3, 0, 8, 0, 255, 255, 255, 127]), "Fallback.apk");
        Check(malformed.Id == "fallback" && malformed.Source == "filename", "malformed optional metadata fallback");
        string data = Path.Combine(root, "rewrite/data");
        var baseline = new ModBase(p => File.ReadAllBytes(p.StartsWith("frontend/") ? Path.Combine(root, "rewrite/Ew4.Web/wwwroot", p[9..]) : Path.Combine(data, p)),
            new[] { "original", "legacy", "client" }.SelectMany(part => Directory.EnumerateFiles(Path.Combine(data, part), "*", SearchOption.AllDirectories)
                .Select(p => Path.GetRelativePath(data, p).Replace('\\', '/'))));
        var store = new ModStore(baseline, Path.Combine(run, "mods"), Path.Combine(run, "saves"));
        string stage = Path.Combine(run, "source"); Directory.CreateDirectory(stage);
        store.Seal(stage, "manifest-test", "清单名称", "1", ["conquest1"]);
        string native = Archive("mod.json", File.ReadAllBytes(Path.Combine(stage, "mod.json")));
        var prepared = store.PrepareImport(native, "wrong.ew4mod");
        Check(prepared.Identity.Id == "manifest-test" && prepared.Identity.Name == "清单名称" && prepared.Identity.Source == "manifest", "native manifest priority");
        Check(store.List().Length == 0, "preparing never installs");
        Check(JsonNode.Parse(store.Dispatch("/api/mods", JsonSerializer.Serialize(new { op = "import", token = prepared.Token, id = "../bad", name = "test" })))!["error"] != null, "invalid edited identifier rejected");
        store.Dispatch("/api/mods", JsonSerializer.Serialize(new { op = "import", token = prepared.Token, id = "edited-mod", name = "手动名称" }));
        Check(SpinWait.SpinUntil(() => JsonNode.Parse(store.Status())!["import"]!["status"]!.GetValue<string>() != "working", TimeSpan.FromSeconds(60)), "import timed out");
        Check(store.List().Single().Id == "edited-mod" && store.List().Single().Name == "手动名称", "manual edits used by native import");
        Check(JsonNode.Parse(store.Dispatch("/api/mods", JsonSerializer.Serialize(new { op = "import", token = prepared.Token, id = "duplicate", name = "duplicate" })))!["error"] != null, "token cannot be reused");
        var discarded = store.PrepareImport(Archive("assets/test.txt", [1]), "unused.zip");
        store.Dispatch("/api/mods", JsonSerializer.Serialize(new { op = "discard", token = discarded.Token }));
        Check(!Directory.EnumerateFiles(store.Root, ".pending-*").Any(), "pending files cleaned");
        var real = new List<ModImportIdentity>();
        foreach (string path in Directory.EnumerateFiles(root, "*.apk")) real.Add(ModIdentityReader.Read(path, Path.GetFileName(path)));
        Check(real.All(i => i.Source == "apk"), "real APK package metadata");
        File.WriteAllText(Path.Combine(run, "results.json"), JsonSerializer.Serialize(new { checks, errors = 0, realApks = real, run }));
        return checks;
    }
    private static byte[] BinaryManifest(bool utf8)
    {
        string[] strings = ["manifest", "package", "com.example.binary", "application", "label", "测试应用"];
        using var pool = new MemoryStream(); var offsets = new List<int>();
        foreach (string text in strings)
        {
            offsets.Add((int)pool.Length); using var writer = new BinaryWriter(pool, Encoding.UTF8, true);
            var bytes = (utf8 ? Encoding.UTF8 : Encoding.Unicode).GetBytes(text);
            if (utf8) { writer.Write((byte)text.Length); writer.Write((byte)bytes.Length); writer.Write(bytes); writer.Write((byte)0); }
            else { writer.Write((ushort)text.Length); writer.Write(bytes); writer.Write((ushort)0); }
        }
        using var output = new MemoryStream(); using var w = new BinaryWriter(output);
        w.Write((ushort)3); w.Write((ushort)8); w.Write(0);
        w.Write((ushort)1); w.Write((ushort)28); w.Write(28 + offsets.Count * 4 + (int)pool.Length);
        w.Write(offsets.Count); w.Write(0); w.Write(utf8 ? 0x100 : 0); w.Write(28 + offsets.Count * 4); w.Write(0);
        foreach (int offset in offsets) w.Write(offset); w.Write(pool.ToArray());
        foreach (var (element, key, value) in new[] { (0, 1, 2), (3, 4, 5) })
        {
            w.Write((ushort)0x102); w.Write((ushort)16); w.Write(56); w.Write(1); w.Write(-1);
            w.Write(-1); w.Write(element); w.Write((ushort)20); w.Write((ushort)20); w.Write((ushort)1); w.Write((ushort)0); w.Write((ushort)0); w.Write((ushort)0);
            w.Write(-1); w.Write(key); w.Write(value); w.Write((ushort)8); w.Write((byte)0); w.Write((byte)3); w.Write(value);
        }
        output.Position = 4; w.Write((int)output.Length); return output.ToArray();
    }
}
