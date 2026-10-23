using Ew4.Platform.Engine;

namespace Ew4.Platform.Content.Original;

public static class OriginalEquipmentCorrections
{
    private const string OriginalItemHash = "ded737008eef2b8fefd1347d65d0e3fc9f051e82eb88f71bf2200247e9b80adc";
    private static readonly IReadOnlyDictionary<string, int> Values = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        ["Tent Tentage"] = 4, ["Army Tentage"] = 8, ["Military Camp"] = 12,
        ["Combat Uniform"] = 6, ["Bearskin Cap"] = 3,
        ["Ferguson Rifle"] = 8, ["Baker Rifle"] = 6, ["Brown Bess"] = 4, ["Bayonet"] = 2,
        ["Gilding Cuirass"] = 6, ["Cuirass"] = 3,
        ["Cavalry Pistol"] = 8, ["Cavalry Sword"] = 6, ["Blucher Sabre"] = 4, ["Trooper Sabre"] = 2,
        ["Helmet"] = 6, ["Canvas Coat"] = 3,
        ["Potent Gunpowder"] = 8, ["Powder Mixture"] = 6, ["Pressed Powder"] = 4, ["Brown Gunpowder"] = 2,
        ["Figurehead"] = 4, ["Propeller"] = 2, ["Bow Chaser"] = 6, ["Ram Stem"] = 4, ["Concrete"] = 4,
        ["Eagle Flag"] = 4, ["Bear Flag"] = 2, ["Dragon Flag"] = 4,
        ["Lions Flag"] = 4, ["Tiger Flag"] = 2, ["Wolf Flag"] = 2
    };

    public static bool IsOriginal(string hash) => string.Equals(hash, OriginalItemHash, StringComparison.OrdinalIgnoreCase);

    public static Equipment Apply(Equipment item, string name)
    {
        // Keep raw imported assets intact; correct the known base game's runtime semantics.
        string effect = item.Effect == Ew4Commander.Effect(1) ? Ew4Commander.Effect(2)
            : item.Effect == Ew4Commander.Effect(2) ? Ew4Commander.Effect(1) : item.Effect;
        return item with { Effect = effect, Value = Values.GetValueOrDefault(name, item.Value) };
    }
}
