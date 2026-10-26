using System.Text;
using System.Text.Json;
using Ew4.Platform.Mod.Scenario;

namespace Ew4.Platform.Editor.Campaign;

public sealed record RoundtripResult(bool Stable, int SourceLength, int ExportedLength);

/// <summary>经 Mod 的转换器检查场景 JSON 能否稳定地换成游戏二进制格式再换回；Editor 只经转换器接口接触该格式。</summary>
public static class ScenarioRoundtrip
{
    public static RoundtripResult CheckJson(IScenarioBtlConverter converter, string scenarioId, byte[] json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        byte[] exported = converter.ToBtl(document.RootElement, new()).Bytes!;
        return CheckBinary(converter, scenarioId, exported);
    }

    public static RoundtripResult CheckBinary(IScenarioBtlConverter converter, string scenarioId, byte[] source)
    {
        string json = converter.ToJson(scenarioId, source, new()).Json!;
        using JsonDocument document = JsonDocument.Parse(json);
        byte[] exported = converter.ToBtl(document.RootElement, new()).Bytes!;
        return new(exported.AsSpan().SequenceEqual(source), source.Length, exported.Length);
    }
}
