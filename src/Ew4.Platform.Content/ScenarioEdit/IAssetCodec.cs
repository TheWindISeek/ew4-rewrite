namespace Ew4.Platform.Content.ScenarioEdit;

/// <summary>
/// 单一资产类型的 Import/Export 抽象；BTL、XML、BIN 各自实现，供 Editor 与 CLI 共用。
/// </summary>
public interface IAssetCodec<TModel>
{
    string LogicalKind { get; }
    TModel Import(byte[] raw);
    byte[] Export(TModel model);
}
