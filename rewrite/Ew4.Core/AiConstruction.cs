using System;
using System.Collections.Generic;
using System.Linq;

namespace Ew4.Core;

public sealed record AiStockAdmission(long DrawBound, long PerStock, long StockCap)
{
    public void Validate()
    {
        if (DrawBound < 1 || PerStock < 0 || StockCap < 0 || PerStock > 0 && StockCap > DrawBound / PerStock)
            throw new RuleViolation("ai.construction_admission", "Invalid construction admission policy");
    }
}
public sealed record AiEnemyRandomBonus(string Category, int DrawBound);
public sealed record AiInstallationRecipe(string Id, IReadOnlyList<AiEnemyRandomBonus> Bonuses);
public sealed record AiInstallationRules
{
    public IReadOnlyList<AiInstallationRecipe> Recipes { get; init; } = Array.Empty<AiInstallationRecipe>();
    public IReadOnlyList<string> PreferenceOrder { get; init; } = Array.Empty<string>();
    public long PreferredBase { get; init; } = -1;
    public long OtherBase { get; init; } = -20;
    public long MinimumScore { get; init; }
    public AiStockAdmission Admission { get; init; } = new(100, 10, 10);
    public void Validate()
    {
        Admission.Validate();
        if (Recipes.Count == 0 || Recipes.Any(r => string.IsNullOrWhiteSpace(r.Id)
                || r.Bonuses.Any(b => string.IsNullOrWhiteSpace(b.Category) || b.DrawBound < 1))
            || Recipes.Select(r => r.Id).Distinct(StringComparer.Ordinal).Count() != Recipes.Count
            || PreferenceOrder.Count == 0 || PreferenceOrder.Any(id => !Recipes.Any(r => r.Id == id)))
            throw new RuleViolation("ai.installation_configuration", "Invalid installation recipes or preference order");
    }
}
public sealed record AiInstallationSite(int Cell, bool Eligible, IReadOnlyDictionary<string, int> Enemies,
    IReadOnlyCollection<string>? AllowedRecipes = null);
public sealed record AiConstructionChoice(int Cell, string RecipeId, long Score);
public sealed record AiFortressRecipe(string Id, long Weight, bool Coastal);
public sealed record AiFortressRules
{
    public IReadOnlyList<AiFortressRecipe> Recipes { get; init; } = Array.Empty<AiFortressRecipe>();
    public long CoastalBase { get; init; } = 5;
    public long LandBonus { get; init; } = 6;
    public long NavalEnemyBonus { get; init; } = 15;
    public long FriendlySeaBonus { get; init; } = 3;
    public long NearbyBase { get; init; } = 3;
    public long NearbyLevelBonus { get; init; } = 3;
    public long NearbyLandBonus { get; init; } = 6;
    public long EnemyPenalty { get; init; } = 3;
    public long MinimumScore { get; init; } = 12;
    public AiStockAdmission Admission { get; init; } = new(60, 10, 6);
    public void Validate()
    {
        Admission.Validate();
        if (Recipes.Count == 0 || Recipes.Any(r => string.IsNullOrWhiteSpace(r.Id) || r.Weight < 1)
            || Recipes.Select(r => r.Id).Distinct(StringComparer.Ordinal).Count() != Recipes.Count
            || new[] { CoastalBase, LandBonus, NavalEnemyBonus, FriendlySeaBonus, NearbyBase, NearbyLevelBonus, NearbyLandBonus, EnemyPenalty }.Any(v => v < 0))
            throw new RuleViolation("ai.fortress_configuration", "Invalid fortress recipes or scoring");
    }
}
public sealed record AiFortressNearby(bool Allied, int? BuildingLevel, bool Sea);
public sealed record AiFortressSite(int Cell, bool Eligible, bool Coastal, bool Sea, int NearbyEnemies, int NavalEnemies, int FriendlySea,
    IReadOnlyList<AiFortressNearby> NearbySites, IReadOnlyCollection<string>? AllowedRecipes = null);

public static partial class AiProduction
{
    private static long Stock(IReadOnlyDictionary<string, long> stock, string id) => stock.TryGetValue(id, out long value) ? value : 0;
    private static void CheckStock(IReadOnlyDictionary<string, long> stock)
    {
        if (stock.Any(p => string.IsNullOrWhiteSpace(p.Key) || p.Value < 0)) throw new RuleViolation("ai.construction_stock", "Invalid construction affordability counts");
    }
    private static bool AdmitConstruction(long stock, AiStockAdmission rules, Func<long, long> draw) =>
        Draw(draw, rules.DrawBound) >= rules.DrawBound - Math.Min(rules.StockCap, stock) * rules.PerStock;

    public static AiConstructionChoice? SelectInstallation(IReadOnlyList<AiInstallationSite> sites, IReadOnlyDictionary<string, long> stock,
        AiInstallationRules rules, Func<long, long> draw)
    {
        rules.Validate(); CheckStock(stock);
        string preferred = rules.PreferenceOrder[(int)Draw(draw, rules.PreferenceOrder.Count)];
        AiConstructionChoice? best = null;
        foreach (var site in sites)
        {
            if (site.Cell < 0 || site.Enemies.Any(p => string.IsNullOrWhiteSpace(p.Key) || p.Value < 0))
                throw new RuleViolation("ai.installation_site", "Invalid installation neighborhood");
            if (!site.Eligible) continue;
            foreach (var recipe in rules.Recipes)
            {
                if (site.AllowedRecipes != null && !site.AllowedRecipes.Contains(recipe.Id)) continue;
                long score = recipe.Id == preferred ? rules.PreferredBase : rules.OtherBase;
                if (Stock(stock, recipe.Id) > 0)
                    foreach (var bonus in recipe.Bonuses)
                    {
                        int count = site.Enemies.TryGetValue(bonus.Category, out int value) ? value : 0;
                        for (int i = 0; i < count; i++) score = checked(score + Draw(draw, bonus.DrawBound));
                    }
                if (score >= rules.MinimumScore && (best == null || score > best.Score)) best = new(site.Cell, recipe.Id, score);
            }
        }
        return best != null && AdmitConstruction(Stock(stock, best.RecipeId), rules.Admission, draw) ? best : null;
    }

    public static AiConstructionChoice? SelectFortress(IReadOnlyList<AiFortressSite> sites, IReadOnlyDictionary<string, long> stock,
        bool enabled, AiFortressRules rules, Func<long, long> draw)
    {
        rules.Validate(); CheckStock(stock);
        if (!enabled) return null;
        long total = 0;
        foreach (var recipe in rules.Recipes) total = checked(total + recipe.Weight);
        long selection = Draw(draw, total);
        var mode = rules.Recipes[0];
        foreach (var recipe in rules.Recipes)
        {
            if (selection < recipe.Weight) { mode = recipe; break; }
            selection -= recipe.Weight;
        }
        AiConstructionChoice? best = null;
        foreach (var site in sites)
        {
            if (site.Cell < 0 || site.NearbyEnemies < 0 || site.NavalEnemies < 0 || site.FriendlySea < 0 || site.NearbySites.Any(n => n.BuildingLevel < 0))
                throw new RuleViolation("ai.fortress_site", "Invalid fortress neighborhood");
            if (!site.Eligible) continue;
            long neighborhood = 0;
            if (!mode.Coastal)
                foreach (var near in site.NearbySites.Where(n => n.Allied))
                    neighborhood = checked(neighborhood + rules.NearbyBase + (near.BuildingLevel.HasValue ? near.BuildingLevel.Value * rules.NearbyLevelBonus : 0)
                        + (near.Sea ? 0 : rules.NearbyLandBonus));
            foreach (var recipe in rules.Recipes)
            {
                if (site.AllowedRecipes != null && !site.AllowedRecipes.Contains(recipe.Id)) continue;
                long score = 0;
                if (mode.Coastal)
                {
                    if (recipe.Id == mode.Id && site.Coastal)
                        score = checked(rules.CoastalBase + (site.Sea ? 0 : rules.LandBonus)
                            + (Stock(stock, recipe.Id) > 0 ? site.NavalEnemies * rules.NavalEnemyBonus : 0) + site.FriendlySea * rules.FriendlySeaBonus);
                }
                else if (!recipe.Coastal)
                    score = checked((recipe.Id == mode.Id ? neighborhood : 0) - site.NearbyEnemies * rules.EnemyPenalty);
                if (best == null || score > best.Score) best = new(site.Cell, recipe.Id, score);
            }
        }
        return best != null && best.Score >= rules.MinimumScore && AdmitConstruction(Stock(stock, best.RecipeId), rules.Admission, draw) ? best : null;
    }
}

public static class LegacyAiConstruction
{
    public static AiInstallationRules Installations() => new()
    {
        Recipes = new AiInstallationRecipe[]
        {
            new("41", new[] { new AiEnemyRandomBonus("infantry", 3) }), new("42", new[] { new AiEnemyRandomBonus("cavalry", 5) }),
            new("43", new[] { new AiEnemyRandomBonus("artillery", 6), new("naval", 6), new("fort", 3) })
        }, PreferenceOrder = new[] { "41", "43", "42" }
    };
    public static AiFortressRules Fortresses() => new()
    {
        Recipes = new AiFortressRecipe[] { new("40", 3, true), new("37", 2, false), new("38", 2, false), new("39", 2, false) }
    };
}
