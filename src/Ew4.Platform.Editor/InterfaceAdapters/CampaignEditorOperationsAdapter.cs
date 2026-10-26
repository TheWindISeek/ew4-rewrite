using System.Text.Json;
using Ew4.Interface.Editor;
using Ew4.Platform.Content;
using Ew4.Platform.Content.Maps;
using Ew4.Platform.Editor.Campaign;
using Ew4.Platform.Mod;
using Ew4.Platform.Mod.Scenario;

namespace Ew4.Platform.Editor.InterfaceAdapters;

public sealed class CampaignEditorOperationsAdapter : ICampaignEditorOperations
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private readonly CampaignDocumentService _documents;
    private readonly IScenarioBtlConverter _converter;
    private readonly IScenarioEditor _editor;

    public CampaignEditorOperationsAdapter(
        CampaignDocumentService documents,
        IScenarioBtlConverter converter,
        IScenarioEditor editor)
    {
        _documents = documents;
        _converter = converter;
        _editor = editor;
    }

    public EditorOutcome<byte[]> GetWorkspaceCampaignJson(string workspaceId, string campaignId) =>
        Try(() => _editor.OpenJson(workspaceId, campaignId));

    public EditorOutcome<string> SaveWorkspaceCampaignJson(string workspaceId, string campaignId, byte[] jsonUtf8, bool smoke) =>
        Try(() => CampaignEditorHttpJson.SaveCampaign(_documents.SaveDocument(workspaceId, campaignId, jsonUtf8, smoke)));

    public EditorOutcome<string> RoundtripWorkspaceJson(string workspaceId, string campaignId) =>
        Try(() => CampaignEditorHttpJson.Roundtrip(_documents.RoundtripWorkspace(workspaceId, campaignId)));

    public EditorOutcome<string> ApplyWorkspaceCampaignEditJson(string workspaceId, string campaignId, byte[] editUtf8) =>
        Try(() => CampaignEditorHttpJson.ApplyCampaign(
            _editor.Apply(workspaceId, campaignId, ScenarioEditHttpJson.Parse(editUtf8))));

    public EditorOutcome<string> CreateWorkspaceCampaignJson(string workspaceId, byte[] createUtf8) =>
        Try(() =>
        {
            using var doc = JsonDocument.Parse(createUtf8);
            JsonElement root = doc.RootElement;
            string id = root.GetProperty("id").GetString() ?? throw new ContentError("scenario.id", "id is required.");
            string name = root.TryGetProperty("name", out var n) && n.ValueKind == JsonValueKind.String ? n.GetString()! : id;
            MapCrop? crop = root.TryGetProperty("crop", out var c) && c.ValueKind == JsonValueKind.Object
                ? new(c.GetProperty("x").GetInt32(), c.GetProperty("y").GetInt32(), c.GetProperty("w").GetInt32(), c.GetProperty("h").GetInt32())
                : null;
            ScenarioSaveResult saved = _editor.Create(workspaceId, id, name, root.GetProperty("mapId").GetInt32(), crop);
            return JsonSerializer.Serialize(new { scenarioId = id, documentPath = saved.DocumentPath }, Web);
        });

    /// <summary>导入入口：上传二进制剧本，经 Mod 转换器得到场景 JSON。</summary>
    public EditorOutcome<byte[]> BtlBytesToDocumentJson(string campaignId, byte[] btlBytes) =>
        Try(() => System.Text.Encoding.UTF8.GetBytes(_converter.ToJson(campaignId, btlBytes, new()).Json!));

    private static EditorOutcome<T> Try<T>(Func<T> action)
    {
        try
        {
            return EditorOutcome<T>.Ok(action());
        }
        catch (ContentError error)
        {
            return EditorOutcome<T>.Fail(error.Code, error.Message);
        }
        catch (ModError error)
        {
            return EditorOutcome<T>.Fail(error.Code, error.Message);
        }
        catch (Exception error) when (error is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            return EditorOutcome<T>.Fail("editor.bad_request", error.Message);
        }
    }
}
