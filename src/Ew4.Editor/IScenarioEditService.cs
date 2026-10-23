using Ew4.Platform.Content;
using Ew4.Platform.Content.ScenarioEdit;

namespace Ew4.Editor;

public sealed record ScenarioSaveResult(string BtlPath, string DocumentPath);

public sealed record RoundtripReport(bool BytesMatch, int OriginalLength, int ExportedLength);

public interface IScenarioEditService
{
    ScenarioDocument OpenFromPack(IPackSource pack, string scenarioId);
    ScenarioSaveResult SaveDocument(string workspaceRoot, ScenarioDocument document);
    RoundtripReport ValidateDocumentRoundtrip(byte[] originalBtl, string scenarioId);
}
