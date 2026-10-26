namespace Ew4.Platform.Mod.Scenario;

public enum ConversionSeverity { Info, Warning, Error }

public sealed record ConversionDiagnostic(ConversionSeverity Severity, string Code, string Path, string Message);

/// <summary>转换上下文：<see cref="Name"/> 为选关显示名（btl 本身不含），预留 base 地图 hash 等信息。</summary>
public sealed record ConvertContext(string? Name = null);

/// <summary>转换结果：<see cref="Json"/> 与 <see cref="Bytes"/> 视方向只有一个有值。</summary>
public sealed record ConvertResult(string? Json, byte[]? Bytes, IReadOnlyList<ConversionDiagnostic> Diagnostics);

/// <summary>
/// btl 与场景 JSON（schema 3）互转。btl 只存在于 Mod 内部：出了 Mod，其它模块只看到 JSON。
/// 输入输出都是字节或 JSON 文本，不引用 Content 的类型。
/// </summary>
public interface IScenarioBtlConverter
{
    ConvertResult ToJson(string scenarioId, ReadOnlyMemory<byte> btl, ConvertContext context);

    ConvertResult ToBtl(System.Text.Json.JsonElement scenario, ConvertContext context);
}
