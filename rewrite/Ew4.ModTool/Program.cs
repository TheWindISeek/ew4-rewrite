using System.Text.Json;
using Ew4.Mods;
using Ew4.ModTool;

try
{
    string? Option(string name) => args.FirstOrDefault(a => a.StartsWith("--" + name + "=", StringComparison.Ordinal))?.Split('=', 2)[1];
    string[] positional = args.Where(a => !a.StartsWith("--", StringComparison.Ordinal)).ToArray();
    if (positional.Length == 0) throw new ArgumentException("命令: import | list | activate | export | pack | verify | jurisdiction-export <源.btl> <输出.json> | jurisdiction-apply <源.btl> <配置.json> <输出.btl> | jurisdiction-strip <源.btl> <输出.btl> | victory-export <源.btl> <输出.json> | victory-apply <源.btl> <规则.json> <输出.btl> | victory-strip <源.btl> <输出.btl>；选项 --id= --name= --version= --base= --mods= --saves= --content=");
    string data = Path.GetFullPath(Option("base") ?? Path.Combine(AppContext.BaseDirectory, "data"));
    byte[] Read(string p) => File.ReadAllBytes(Path.Combine(data, p));
    var baseline = new ModBase(Read, Directory.EnumerateFiles(data, "*", SearchOption.AllDirectories).Select(p => Path.GetRelativePath(data, p).Replace('\\', '/')));
    if (positional[0] is "victory-export" or "victory-apply" or "victory-strip")
    {
        Console.WriteLine(JsonSerializer.Serialize(VictoryCommands.Run(positional, baseline, Option("content")), JurisdictionCommands.Json)); return;
    }
    if (positional[0] is "jurisdiction-export" or "jurisdiction-apply" or "jurisdiction-strip")
    {
        Console.WriteLine(JsonSerializer.Serialize(JurisdictionCommands.Run(positional, baseline, Option("content")), JurisdictionCommands.Json));
        return;
    }
    var store = new ModStore(baseline, Option("mods") ?? Path.Combine(AppContext.BaseDirectory, "mods"), Option("saves") ?? Path.Combine(AppContext.BaseDirectory, "saves"));
    string P(int i) => positional.Length > i ? Path.GetFullPath(positional[i]) : throw new ArgumentException("命令缺少参数");
    object result = positional[0] switch
    {
        "import" => store.Import(P(1), Option("id") ?? "my-mod", Option("name") ?? Path.GetFileNameWithoutExtension(P(1)), p => Console.Error.WriteLine(p.Message)),
        "list" => store.List(),
        "verify" => store.Validate(P(1)),
        _ => Run()
    };
    object Run()
    {
        switch (positional[0])
        {
            case "activate": store.Activate(positional[1] == "builtin" ? null : positional[1]); return new { active = positional[1] };
            case "export": store.Export(positional[1], P(2)); return new { exported = P(2) };
            case "pack":
                store.Seal(P(1), Option("id") ?? "my-mod", Option("name") ?? "我的 Mod", Option("version") ?? "1.0");
                return store.Validate(P(1));
            default: throw new ArgumentException("未知命令: " + positional[0]);
        }
    }
    Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
}
catch (Exception e) { Console.Error.WriteLine(e.Message); Environment.ExitCode = 1; }
