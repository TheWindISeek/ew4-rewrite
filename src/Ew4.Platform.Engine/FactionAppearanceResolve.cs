namespace Ew4.Platform.Engine;

/// <summary>
/// 势力旗、扩展图与地图涂色解析（前后端应对齐同一规则；前端见 <c>appearance.js</c>）。
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
            color = battlePackedColor;
        }

        if (color == 0)
        {
            color = DefaultPackedColor(factionId);
        }

        return new(flag, flagImage, color);
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
