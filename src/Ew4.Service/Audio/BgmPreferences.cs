using System.Text;

namespace Ew4.Service.Audio;

/// <summary>
/// 玩家本机的背景音乐配置：存放在存档根目录的 <c>music.json</c>（与 mod 的曲目表同格式），
/// 优先于 mod 自带的安排。按势力（国家 id）覆盖；<c>default</c> 是玩家的全局默认。
/// </summary>
public sealed class BgmPreferences(string saveRoot)
{
    private string FilePath => Path.Combine(saveRoot, "music.json");

    public BgmCatalog Load()
    {
        try
        {
            return File.Exists(FilePath) ? BgmCatalog.Parse(File.ReadAllText(FilePath, Encoding.UTF8)) : BgmCatalog.Empty;
        }
        catch (Exception error) when (error is IOException or System.Text.Json.JsonException or Ew4.Interface.Common.OperationException)
        {
            return BgmCatalog.Empty; // 损坏的配置不应阻止开局
        }
    }

    /// <summary>设置某国家（<c>null</c> = 全局默认）的曲目；<paramref name="track"/> 为 <c>null</c> 表示清除覆盖。</summary>
    public BgmCatalog Set(string? countryId, string? track)
    {
        BgmCatalog updated = Load().With(countryId, track);
        Directory.CreateDirectory(saveRoot);
        string temp = FilePath + ".tmp";
        File.WriteAllText(temp, updated.ToJson(), new UTF8Encoding(false));
        File.Move(temp, FilePath, overwrite: true);
        return updated;
    }
}
