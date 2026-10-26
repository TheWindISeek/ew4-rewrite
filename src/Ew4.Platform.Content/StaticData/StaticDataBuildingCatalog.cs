using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using Ew4.Platform.Engine;

namespace Ew4.Platform.Content.StaticData;

public sealed record StaticDataBuildingLevel(BuildingTurnRule Turn, int DefensePercent,
    IReadOnlyDictionary<int, int> Recruitment, string Image);
public sealed record StaticDataBuildingDefinition(int StaticDataId, string Kind, int MaximumUpgradeLevel,
    IReadOnlyList<StaticDataBuildingLevel> Levels);
public sealed record StaticDataCapital(int Id, string Name, long MoneyIncome, string Image);

public sealed class StaticDataBuildingCatalog
{
    public static IReadOnlyDictionary<int, string> Kinds { get; } = new ReadOnlyDictionary<int, string>(new Dictionary<int, string>
        { [0] = "city", [1] = "industry", [2] = "stable", [3] = "port", [4] = "farmland" });
    public IReadOnlyDictionary<int, StaticDataBuildingDefinition> Definitions { get; }
    public IReadOnlyDictionary<int, StaticDataCapital> Capitals { get; }
    public string SourceRevision { get; }
    private StaticDataBuildingCatalog(Dictionary<int, StaticDataBuildingDefinition> buildings, Dictionary<int, StaticDataCapital> capitals, string revision)
    {
        Definitions = new ReadOnlyDictionary<int, StaticDataBuildingDefinition>(buildings);
        Capitals = new ReadOnlyDictionary<int, StaticDataCapital>(capitals); SourceRevision = revision;
    }

    public static StaticDataBuildingCatalog Read(string constructionsXml, string capitalsXml)
    {
        var constructionRoot = XDocument.Parse(constructionsXml).Root;
        var capitalRoot = XDocument.Parse(capitalsXml).Root;
        if (constructionRoot?.Name != "constructions" || capitalRoot?.Name != "capitals")
            throw new RuleViolation("legacy.building_definitions", "Unexpected building/capital definition root");
        var buildings = new Dictionary<int, StaticDataBuildingDefinition>();
        foreach (var element in constructionRoot.Elements("construction"))
        {
            string kind = (string?)element.Attribute("type") ?? "";
            int id = Kinds.Where(p => p.Value == kind).Select(p => p.Key).DefaultIfEmpty(-1).Single();
            int maximum = (int?)element.Attribute("maxlevel") ?? 1;
            if (id < 0 || buildings.ContainsKey(id)) throw new RuleViolation("legacy.building_definitions", "Unknown or duplicate legacy building kind");
            var levels = new List<StaticDataBuildingLevel>();
            foreach (var row in element.Elements("level"))
            {
                // Original lookup is one based and follows XML row order; idx is not consulted.
                int level = levels.Count + 1;
                int Integer(string key) => (int?)row.Attribute(key) ?? 0;
                var income = new Resources(Integer("tax"), Integer("industry"), Integer("food")); income.Validate();
                int healing = Integer("supply"), defense = Integer("avoid");
                if (healing < 0 || defense < 0 || defense > 100)
                    throw new RuleViolation("legacy.building_definitions", "Invalid building recovery/defense");
                var recruitment = new Dictionary<int, int>();
                foreach (var recruit in row.Elements("recruit"))
                {
                    string name = (string?)recruit.Attribute("name") ?? "";
                    int grade = (int?)recruit.Attribute("grade") ?? 0;
                    if (grade < 0 || grade > 2 || !recruitment.TryAdd(StaticDataRuntimeCatalog.UnitIndex(name), grade))
                        throw new RuleViolation("legacy.building_recruitment", "Invalid or duplicate legacy building recruitment");
                }
                bool expands = id == 0 && level >= 4 || id == 1 && level >= 3 || id == 2 && level >= 2;
                levels.Add(new(new(kind, level, income, healing, expands), defense,
                    new ReadOnlyDictionary<int, int>(recruitment), (string?)row.Attribute("image") ?? ""));
            }
            if (levels.Count == 0 || maximum < 1 || maximum > levels.Count)
                throw new RuleViolation("legacy.building_definitions", "Building upgrade ceiling has no corresponding row");
            buildings.Add(id, new(id, kind, maximum, levels.AsReadOnly()));
        }
        var capitals = new Dictionary<int, StaticDataCapital>();
        foreach (var element in capitalRoot.Elements("capital"))
        {
            int id = (int?)element.Attribute("id") ?? -1;
            long money = (long?)element.Attribute("tax") ?? 0;
            string name = (string?)element.Attribute("name") ?? "";
            if (id < 0 || money < 0 || string.IsNullOrWhiteSpace(name)
                || !capitals.TryAdd(id, new(id, name, money, (string?)element.Attribute("image") ?? "")))
                throw new RuleViolation("legacy.capital_definition", "Invalid or duplicate capital");
        }
        string revision = Convert.ToHexStringLower(Ew4.Interface.Common.Sha256Hash.HashData(JsonSerializer.SerializeToUtf8Bytes(new[] { constructionsXml, capitalsXml })));
        return new(buildings, capitals, revision);
    }

    public int NormalizeLevel(int kind, int requested)
    {
        if (requested < 0 || !Definitions.TryGetValue(kind, out var definition))
            throw new RuleViolation("legacy.building_kind", "Unknown legacy building or negative level");
        return Math.Min(requested, definition.Levels.Count);
    }

    public StaticDataBuildingLevel? Level(int kind, int level)
    {
        if (!Definitions.TryGetValue(kind, out var definition)) throw new RuleViolation("legacy.building_kind", "Unknown legacy building");
        return level >= 1 && level <= definition.Levels.Count ? definition.Levels[level - 1] : null;
    }

    public bool CanRecruit(int kind, int level, int unit, int grade) => grade >= 0 && grade <= 2
        && Level(kind, level)?.Recruitment.TryGetValue(unit, out int maximum) == true && grade <= maximum;

    public Resources Income(int kind, int level, int capital = -1)
    {
        var income = Level(kind, level)?.Turn.Income ?? new Resources(0, 0, 0);
        return kind == 0 && Capitals.TryGetValue(capital, out var specific) ? income with { Money = specific.MoneyIncome } : income;
    }

    public WorldTurnRules TurnRules(StaticDataBattle battle)
    {
        var sites = new Dictionary<int, BuildingIncomeOverride>();
        foreach (var site in battle.EffectiveSites.Where(s => s.BuildingKind == 0 && s.State8 >= 0))
        {
            // Tax lookup falls back if the capital definition is absent; graphics are handled separately.
            if (Capitals.TryGetValue(site.State8, out var capital))
                if (!sites.TryAdd(site.Cell, new("city", Money: capital.MoneyIncome)))
                    throw new RuleViolation("legacy.site_cell", "Duplicate capital site");
        }
        return new() { Buildings = Definitions.Values.SelectMany(d => new[] { new BuildingTurnRule(d.Kind, 0, new(0, 0, 0)) }
            .Concat(d.Levels.Select(l => l.Turn))).ToArray(), SiteIncomeOverrides = new ReadOnlyDictionary<int, BuildingIncomeOverride>(sites) };
    }

    public BuildingUpgradeRules UpgradeRules(SkillParameters? skillParameters = null)
    {
        long[] money = [65, 60, 80, 75, 40], industry = [0, 20, 5, 10, 0];
        return new() { DiscountPercent = (skillParameters ?? StaticDataSkillParameters.Default).Percent(30),
            Steps = Definitions.Values.SelectMany(d => Enumerable.Range(0, d.MaximumUpgradeLevel)
            .Select(level => new BuildingUpgradeStep(d.Kind, level, level + 1, new(money[d.StaticDataId], industry[d.StaticDataId], 0)))).ToArray() };
    }

    public IReadOnlyList<BuildingDefenseRule> BuildingDefenseRules() => Definitions.Values.SelectMany(d => new[] { new BuildingDefenseRule(d.Kind, 0, 0) }
        .Concat(d.Levels.Select(l => new BuildingDefenseRule(d.Kind, l.Turn.Level, l.DefensePercent)))).ToArray();
}
