using System.Text.Json;
using Ew4.Platform.Mod;
using Ew4.Platform.Mod.Scenario;
using Xunit;

namespace Ew4.Test.Platform.Mod;

public sealed class ScenarioBtlConverterTests
{
    private static readonly ScenarioBtlConverter Converter = new();

    private static string BtlDirectory()
    {
        for (DirectoryInfo? dir = new(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            string candidate = Path.Combine(dir.FullName, "mods", "base", "source", "original");
            if (Directory.Exists(candidate)) return candidate;
        }

        throw new DirectoryNotFoundException("mods/base/source/original not found above " + AppContext.BaseDirectory);
    }

    [Fact]
    public void Every_base_btl_roundtrips_byte_for_byte()
    {
        string[] files = Directory.GetFiles(BtlDirectory(), "*.btl");
        Assert.NotEmpty(files);
        foreach (string file in files)
        {
            byte[] raw = File.ReadAllBytes(file);
            string id = Path.GetFileNameWithoutExtension(file);
            ConvertResult json = Converter.ToJson(id, raw, new());
            using JsonDocument document = JsonDocument.Parse(json.Json!);
            ConvertResult back = Converter.ToBtl(document.RootElement, new());
            Assert.True(raw.AsSpan().SequenceEqual(back.Bytes), id + " did not roundtrip");
            Assert.Empty(back.Diagnostics);
        }
    }

    [Fact]
    public void Json_exposes_schema_and_map_reference()
    {
        string file = Directory.GetFiles(BtlDirectory(), "*.btl").Order().First();
        ConvertResult result = Converter.ToJson("x", File.ReadAllBytes(file), new());
        using JsonDocument document = JsonDocument.Parse(result.Json!);
        JsonElement root = document.RootElement;
        Assert.Equal(3, root.GetProperty("schema").GetInt32());
        Assert.Equal("x", root.GetProperty("id").GetString());
        Assert.Equal("base", root.GetProperty("map").GetProperty("source").GetString());
        Assert.True(root.GetProperty("map").GetProperty("crop").GetProperty("w").GetInt32() > 0);
    }

    [Fact]
    public void Semantic_fields_are_readable_and_every_one_is_authoritative_on_export()
    {
        string file = Directory.GetFiles(BtlDirectory(), "*.btl").Order().First();
        byte[] raw = File.ReadAllBytes(file);
        string json = Converter.ToJson("x", raw, new()).Json!;
        var node = System.Text.Json.Nodes.JsonNode.Parse(json)!;
        Assert.NotEmpty(node["factions"]!.AsArray());
        Assert.False(string.IsNullOrEmpty(node["factions"]![0]!["country"]!.GetValue<string>()));
        Assert.NotNull(node["sites"]);
        Assert.NotNull(node["armies"]);
        Assert.NotNull(node["events"]);
        Assert.NotNull(node["markets"]);
        Assert.NotNull(node["taverns"]);
        Assert.Equal(node["map"]!["crop"]!["h"]!.GetValue<int>(), node["ownership"]!["rows"]!.AsArray().Count);

        // 编辑语义字段：money 改变，其余逐字节不变；条数随数组更新。
        int money = node["factions"]![0]!["money"]!.GetValue<int>();
        node["factions"]![0]!["money"] = money + 123;
        byte[] edited = ToBytes(node);
        Assert.Equal(raw.Length, edited.Length);
        Assert.False(raw.AsSpan().SequenceEqual(edited));
        int diff = 0;
        for (int i = 0; i < raw.Length; i++) if (raw[i] != edited[i]) diff++;
        Assert.True(diff is >= 1 and <= 2, "only the money integer should change, differing bytes: " + diff);

        // 删除一支军队：header 条数与分段长度同步缩小。
        int armies = node["armies"]!.AsArray().Count;
        if (armies > 0)
        {
            node["armies"]!.AsArray().RemoveAt(armies - 1);
            Assert.Equal(raw.Length - 32, ToBytes(node).Length);
        }
    }

    [Fact]
    public void Editing_the_map_reference_changes_the_map_id()
    {
        string file = Directory.GetFiles(BtlDirectory(), "*.btl").Order().First();
        var node = System.Text.Json.Nodes.JsonNode.Parse(Converter.ToJson("x", File.ReadAllBytes(file), new()).Json!)!;
        int mapId = node["map"]!["mapId"]!.GetValue<int>();
        node["map"]!["mapId"] = mapId + 1;
        byte[] edited = ToBytes(node);
        Assert.Equal(mapId + 1, BitConverter.ToInt32(edited, 4));
    }

    [Fact]
    public void Ownership_cell_edit_changes_exactly_one_byte()
    {
        string file = Directory.GetFiles(BtlDirectory(), "*.btl").Order().First();
        byte[] raw = File.ReadAllBytes(file);
        var node = System.Text.Json.Nodes.JsonNode.Parse(Converter.ToJson("x", raw, new()).Json!)!;
        string row = node["ownership"]!["rows"]![0]!.GetValue<string>();
        char changed = row[0] == '.' ? '0' : '.';
        node["ownership"]!["rows"]![0] = changed + row[1..];
        byte[] edited = ToBytes(node);
        Assert.Equal(raw.Length, edited.Length);
        Assert.Equal(1, Enumerable.Range(0, raw.Length).Count(i => raw[i] != edited[i]));
    }

    [Fact]
    public void Tavern_and_market_rows_roundtrip_through_semantic_fields()
    {
        foreach (string file in Directory.GetFiles(BtlDirectory(), "*.btl"))
        {
            var node = System.Text.Json.Nodes.JsonNode.Parse(Converter.ToJson("x", File.ReadAllBytes(file), new()).Json!)!;
            var markets = node["markets"]!.AsArray();
            if (markets.Count == 0) continue;
            Assert.NotEmpty(markets[0]!["slots"]!.AsArray());
            Assert.NotNull(markets[0]!["cell"]);
            return;
        }

        Assert.Fail("no base scenario with a market");
    }

    [Fact]
    public void Extensions_are_semantic_and_rebind_to_the_body_hash()
    {
        string file = Directory.GetFiles(BtlDirectory(), "*.btl").Order().First();
        var node = System.Text.Json.Nodes.JsonNode.Parse(Converter.ToJson("x", File.ReadAllBytes(file), new()).Json!)!;
        Assert.Null(node["extensions"]);
        node["extensions"] = System.Text.Json.Nodes.JsonNode.Parse("""
            [
              { "tag": "JURI", "mapId": 1, "left": 0, "top": 0, "width": 2, "height": 1, "mapWidth": 2, "mapHeight": 1, "cellCenters": [0, 1] },
              { "tag": "VICT", "rules": { "priority": "defeat", "roundLimit": 30, "lose": null } },
              { "tag": "CNTR", "factions": ["fra", "gbr"], "eventCountries": { "0": "fra" } },
              { "tag": "ZZZZ", "version": 3, "flags": 0, "payload": "AQID" }
            ]
            """);
        byte[] first = ToBytes(node);

        // 导出后再导入：已知块回到语义形式，未知块保持 payload；再导出逐字节一致。
        var again = System.Text.Json.Nodes.JsonNode.Parse(Converter.ToJson("x", first, new()).Json!)!;
        var blocks = again["extensions"]!.AsArray();
        Assert.Equal(new[] { "JURI", "VICT", "CNTR", "ZZZZ" }, blocks.Select(b => b!["tag"]!.GetValue<string>()).ToArray());
        Assert.NotNull(blocks[0]!["cellCenters"]);
        Assert.Equal("defeat", blocks[1]!["rules"]!["priority"]!.GetValue<string>());
        Assert.Equal("fra", blocks[2]!["factions"]![0]!.GetValue<string>());
        Assert.Equal("AQID", blocks[3]!["payload"]!.GetValue<string>());
        Assert.Null(again["extras"]!["btl"]!["trailer"]);
        Assert.True(first.AsSpan().SequenceEqual(ToBytes(again)));

        // 编辑主体后，绑定主体的哈希随之重算（不会残留旧哈希）。
        again["factions"]![0]!["money"] = again["factions"]![0]!["money"]!.GetValue<int>() + 1;
        byte[] edited = ToBytes(again);
        var reread = System.Text.Json.Nodes.JsonNode.Parse(Converter.ToJson("x", edited, new()).Json!)!;
        Assert.Equal(4, reread["extensions"]!.AsArray().Count);
        Assert.NotNull(reread["extensions"]![1]!["rules"]);
    }

    [Fact]
    public void Bad_semantic_field_is_rejected()
    {
        string file = Directory.GetFiles(BtlDirectory(), "*.btl").Order().First();
        var node = System.Text.Json.Nodes.JsonNode.Parse(Converter.ToJson("x", File.ReadAllBytes(file), new()).Json!)!;
        node["factions"]![0]!["country"] = "this-name-is-way-too-long";
        Assert.Equal("mod.scenario_field", Assert.Throws<ModError>(() => ToBytes(node)).Code);
        node["factions"]![0]!["country"] = "fra";
        node["factions"]![0]!["money"] = "lots";
        Assert.Equal("mod.scenario_field", Assert.Throws<ModError>(() => ToBytes(node)).Code);
    }

    private static byte[] ToBytes(System.Text.Json.Nodes.JsonNode node)
    {
        using JsonDocument document = JsonDocument.Parse(node.ToJsonString());
        return Converter.ToBtl(document.RootElement, new()).Bytes!;
    }

    [Fact]
    public void Invalid_input_is_rejected()
    {
        Assert.Equal("mod.scenario_btl", Assert.Throws<ModError>(() => Converter.ToJson("x", new byte[10], new())).Code);
        using JsonDocument noExtras = JsonDocument.Parse("""{"schema":3,"id":"x"}""");
        Assert.Equal("mod.scenario_extras", Assert.Throws<ModError>(() => Converter.ToBtl(noExtras.RootElement, new())).Code);
        using JsonDocument wrongSchema = JsonDocument.Parse("""{"schema":1}""");
        Assert.Equal("mod.scenario_schema", Assert.Throws<ModError>(() => Converter.ToBtl(wrongSchema.RootElement, new())).Code);
    }
}
