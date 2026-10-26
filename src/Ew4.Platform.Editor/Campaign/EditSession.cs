using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Ew4.Interface.Common;
using Ew4.Platform.Content;
using Ew4.Platform.Content.Maps;
using Ew4.Platform.Content.StaticData;

namespace Ew4.Platform.Editor.Campaign;

/// <summary>一份场景 JSON（schema 3）的编辑会话：编辑只作用于 JSON，按 <see cref="Capabilities"/> 校验，每次编辑后整体校验。</summary>
public sealed class EditSession
{
    private static readonly JsonSerializerOptions Output = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private JsonObject _document;
    private readonly IMapCatalog? _maps;

    private EditSession(string scenarioId, EditorMode mode, JsonObject document, IMapCatalog? maps)
    {
        _maps = maps;
        ScenarioId = scenarioId;
        Mode = mode;
        Capabilities = Capabilities.For(mode);
        _document = document;
    }

    public string ScenarioId { get; }

    public EditorMode Mode { get; }

    public Capabilities Capabilities { get; }

    public JsonObject Document => _document;

    public static EditSession Open(IModSource pack, string scenarioId, EditorMode mode = EditorMode.BaseTweak, IMapCatalog? maps = null)
    {
        string path = ScenarioModAccess.ScenarioJsonPath(scenarioId);
        if (!pack.Exists(path)) throw new ContentError("scenario.missing", "Scenario not found: " + scenarioId);
        return FromJson(pack.Read(path), scenarioId, mode, maps);
    }

    public static EditSession FromJson(byte[] utf8, string scenarioId, EditorMode mode = EditorMode.BaseTweak, IMapCatalog? maps = null)
    {
        JsonObject document;
        try { document = JsonNode.Parse(utf8) as JsonObject ?? throw new ContentError("scenario.document", "Scenario must be a JSON object."); }
        catch (JsonException e) { throw new ContentError("scenario.document", "Scenario is not valid JSON: " + e.Message); }
        if (document["schema"]?.GetValue<int>() != 3)
            throw new ContentError("scenario.schema", "Scenario '" + scenarioId + "' is not schema 3; convert it with `scenario to-v3`.");
        string? id = document["id"]?.GetValue<string>();
        if (!string.Equals(id, scenarioId, StringComparison.Ordinal))
            throw new ContentError("scenario.id", $"Scenario id '{id}' does not match '{scenarioId}'.");
        ValidateOrThrow(document, maps, scenarioId);
        return new EditSession(scenarioId, mode, document, maps);
    }

    private static readonly System.Text.RegularExpressions.Regex IdPattern = new("^[a-z][a-z0-9_]{0,63}$");

    /// <summary>自由编辑：新建只有地图引用和全无主归属的空场景；地图只能取自 base 的地图目录。</summary>
    public static EditSession CreateFree(string scenarioId, string name, int mapId, MapCrop? crop, IMapCatalog maps)
    {
        if (!Capabilities.For(EditorMode.Free).CreateScenario) throw new ContentError("editor.capability", "This editor session may not create scenarios.");
        if (!IdPattern.IsMatch(scenarioId)) throw new ContentError("scenario.id", "Scenario id must match [a-z][a-z0-9_]{0,63}.");
        var document = new JsonObject
        {
            ["schema"] = 3,
            ["id"] = scenarioId,
            ["name"] = name,
            ["map"] = new JsonObject { ["source"] = "base", ["mapId"] = mapId, ["hash"] = "", ["crop"] = new JsonObject { ["x"] = 0, ["y"] = 0, ["w"] = 1, ["h"] = 1 } },
            ["settings"] = new JsonObject { ["objectivesDisabled"] = 0, ["roundLimit"] = 30, ["ratingRound"] = 0 },
            ["ownership"] = ScenarioJsonPatch.EmptyOwnership(1, 1),
            ["factions"] = new JsonArray(), ["sites"] = new JsonArray(), ["armies"] = new JsonArray(), ["events"] = new JsonArray(),
            ["markets"] = new JsonArray(), ["taverns"] = new JsonArray(), ["environments"] = new JsonArray(),
        };
        ScenarioJsonPatch.Apply(document, new ScenarioEditMapSelect(mapId, crop), Capabilities.For(EditorMode.Free), maps);
        ValidateOrThrow(document, maps, scenarioId);
        return new EditSession(scenarioId, EditorMode.Free, document, maps);
    }

    public void Apply(ScenarioEdit edit)
    {
        var next = (JsonObject)_document.DeepClone();
        ScenarioJsonPatch.Apply(next, edit, Capabilities, _maps);
        ValidateOrThrow(next, _maps, ScenarioId);
        _document = next;
    }

    /// <summary>全部诊断：Error 为必须修复，Warning（如地图 hash 变化）不阻止使用。</summary>
    public IReadOnlyList<Diagnostic> Validate()
    {
        string path = "scenarios/" + ScenarioId + ".json";
        var result = new List<Diagnostic>(ScenarioChecks.Map(_document, _maps, path));
        try
        {
            ValidateBattle(_document);
        }
        catch (ContentError e)
        {
            result.Add(new Diagnostic(Severity.Error, e.Code, path, e.Message));
        }

        return result;
    }

    public byte[] ToUtf8() => JsonSerializer.SerializeToUtf8Bytes(_document, Output);

    private static void ValidateOrThrow(JsonObject document, IMapCatalog? maps, string scenarioId)
    {
        Diagnostic? error = ScenarioChecks.Map(document, maps, "scenarios/" + scenarioId + ".json").FirstOrDefault(d => d.Severity == Severity.Error);
        if (error != null) throw new ContentError(error.Code, error.Message);
        ValidateBattle(document);
    }

    private static void ValidateBattle(JsonObject document)
    {
        try
        {
            using JsonDocument parsed = JsonDocument.Parse(document.ToJsonString());
            StaticDataBattle.FromJson(parsed.RootElement);
        }
        catch (ContentError) { throw; }
        catch (Exception e) when (e is Ew4.Platform.Engine.RuleViolation or JsonException or InvalidCastException)
        {
            throw new ContentError("scenario.invalid", e.Message);
        }
    }
}
