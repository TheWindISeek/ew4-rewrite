using System.Collections.ObjectModel;
using System.Globalization;
using Ew4.Core;

namespace Ew4.Legacy;

public sealed record LegacyFactionRules(IReadOnlyDictionary<int, int> RecruitmentVeterancy, Resources IncomeBonus, int TransportLevel)
{
    public static LegacyFactionRules Read(LegacyFaction source)
    {
        if (source.Values68.Count != 26) throw new RuleViolation("legacy.faction_table", "Expected 26 legacy faction rule entries");
        var recruits = new Dictionary<int, int>();
        for (int unit = 0; unit < 22; unit++) if (source.Values68[unit] >= 0) recruits.Add(unit, source.Values68[unit]);
        long Bonus(int index, int step) => source.Values68[index] is >= 1 and <= 3 ? source.Values68[index] * step : 0;
        return new(new ReadOnlyDictionary<int, int>(recruits), new(Bonus(23, 20), Bonus(24, 10), Bonus(22, 10)), source.Values68[25]);
    }

    public static FactionState Import(LegacyFaction source, string id, string name, string camp,
        LegacyRecruitmentCatalog cards, LegacyRuntimeCatalog runtime)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(camp)
            || source.State176 < 0 || source.SurvivalMode < 0
            || !float.IsFinite(source.IncomeMultiplier) || source.IncomeMultiplier < 0
            || !float.IsFinite(source.AiEvasionBonus) || source.AiEvasionBonus < 0 || source.AiEvasionBonus > 1)
            throw new RuleViolation("legacy.faction_state", $"Invalid faction {id}: name={name}, camp={camp}, shortage={source.State176}, survival={source.SurvivalMode}, income={source.IncomeMultiplier}, evasion={source.AiEvasionBonus}");
        if (source.Resources.Money < int.MinValue || source.Resources.Industry < int.MinValue || source.Resources.Food < int.MinValue)
            throw new RuleViolation("legacy.faction_state", "Opening resource debt exceeds signed BTL values");
        var rules = Read(source);
        var grants = new Dictionary<string, RecruitmentGrant>(StringComparer.Ordinal);
        foreach (var card in cards.Cards.Values.Where(c => c.Recruits))
        {
            if (!rules.RecruitmentVeterancy.TryGetValue(card.Unit, out int veteran)
                || !runtime.HasUnit(source.Variant, card.Unit, card.Grade)) continue;
            string category = runtime.Unit(source.Variant, card.Unit, card.Grade).Profile.Category;
            bool transport = card.Type != "fortress" && (category == "infantry" && rules.TransportLevel > 0
                || category == "cavalry" && rules.TransportLevel > 1 || category == "artillery" && rules.TransportLevel > 2);
            grants.Add(card.Id, new(veteran.ToString(CultureInfo.InvariantCulture), transport));
        }
        return new() { Id = id, Name = name, Camp = camp, Human = !source.Ai, Resources = source.Resources,
            Shortage = source.State176, IncomeMultiplier = source.IncomeMultiplier, AiEvasionBonus = source.AiEvasionBonus,
            IncomeBonus = rules.IncomeBonus, Recruitment = new ReadOnlyDictionary<string, RecruitmentGrant>(grants),
            Survival = FactionSurvival.Legacy(source.SurvivalMode) };
    }
}
