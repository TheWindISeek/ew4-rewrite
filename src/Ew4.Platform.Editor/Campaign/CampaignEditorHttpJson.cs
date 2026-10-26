using System.Text.Json;
using Ew4.Platform.Content;

namespace Ew4.Platform.Editor.Campaign;

internal static class CampaignEditorHttpJson
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    public static string Roundtrip(RoundtripResult result) =>
        JsonSerializer.Serialize(new { result.Stable, result.SourceLength, result.ExportedLength }, Web);

    public static string SaveCampaign(CampaignSaveOutcome outcome) =>
        JsonSerializer.Serialize(new
        {
            outcome.Saved.DocumentPath,
            diagnostics = outcome.SmokeDiagnostics?.Select(ToDiagnosticDto).ToArray(),
        }, Web);

    public static string ApplyCampaign(ScenarioEditResult result) =>
        JsonSerializer.Serialize(new
        {
            result.ScenarioId,
            result.Saved.DocumentPath,
        }, Web);

    private static object ToDiagnosticDto(Diagnostic diagnostic) => new
    {
        severity = diagnostic.Severity.ToString(),
        code = diagnostic.Code,
        path = diagnostic.Path,
        message = diagnostic.Message,
        hint = diagnostic.Hint,
    };
}
