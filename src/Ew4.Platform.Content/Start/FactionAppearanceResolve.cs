using Ew4.Platform.Engine;

namespace Ew4.Platform.Content;

/// <summary>
/// 势力旗、扩展图与地图涂色解析（后端为唯一实现，结果经 API 下发；前端不再复制此规则）。
/// </summary>
public static class FactionAppearanceResolve
{
    public sealed record Resolved(string? Flag, string? FlagImage, int Color);

    public static Resolved Resolve(
        FactionAppearance? appearance,
        string? countryFlag,
        string? countryFlagImage,
        string? countryColorHex,
        string? battleVariant,
        int battlePackedColor,
        string factionId)
    {
        string? flag;
        string? flagImage;
        if (appearance?.Flag != null)
        {
            flag = appearance.Flag;
            flagImage = appearance.FlagImage;
        }
        else if (appearance?.FlagImage != null)
        {
            flag = null;
            flagImage = appearance.FlagImage;
        }
        else
        {
            flag = countryFlag ?? battleVariant;
            flagImage = countryFlagImage;
        }

        int color = 0;
        if (appearance?.Color is string appearanceColor)
        {
            color = ScenarioFactions.PackedColor(appearanceColor);
        }
        else if (countryColorHex is string hex)
        {
            color = ScenarioFactions.PackedColor(hex);
        }
        else if (battlePackedColor != 0)
        {
            color = PackBattleColor(battlePackedColor);
        }

        if (color == 0)
        {
            color = DefaultPackedColor(factionId);
        }

        return new(flag, flagImage, color);
    }

    /// <summary>BTL/剧本 JSON 的 32 位色：<c>0xAARRGGBB</c>。RGB 全 0 且 A&gt;0（如普鲁士 <c>0x28000000</c>）为原版半透明叠色，不是「未指定」。</summary>
    public static int PackBattleColor(int value)
    {
        if (value == 0)
        {
            return 0;
        }

        int a = (value >> 24) & 0xFF;
        int r = (value >> 16) & 0xFF;
        int g = (value >> 8) & 0xFF;
        int b = value & 0xFF;
        if (a == 0 && (r | g | b) != 0)
        {
            a = 255;
        }

        return unchecked(r | g << 8 | b << 16 | a << 24);
    }

    public static int DefaultPackedColor(string factionId)
    {
        uint hash = 2166136261;
        foreach (char c in factionId)
        {
            hash = (hash ^ c) * 16777619;
        }

        int r = (int)(hash & 0xFF);
        int g = (int)((hash >> 8) & 0xFF);
        int b = (int)((hash >> 16) & 0xFF);
        return unchecked(r | g << 8 | b << 16 | 255 << 24);
    }
}
