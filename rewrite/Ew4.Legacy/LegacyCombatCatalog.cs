using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using Ew4.Core;

namespace Ew4.Legacy;

public sealed record LegacyVeterancyLevel(int Armor, int TurnHealing, int PromotionHealing, int ExperienceCost);

public sealed class LegacyCombatCatalog
{
    public IReadOnlyList<LegacyVeterancyLevel> Veterancy { get; }
    public ProgressionRules UnitProgression { get; }
    public ProgressionRules RankProgression { get; }
    public ProgressionRules TitleProgression { get; }
    public TrainingRules Training { get; }
    public IReadOnlyDictionary<string, int> RankHealth { get; }
    public IReadOnlyDictionary<string, int> TitleHealing { get; }
    public CombatContextRules Context { get; }
    public string NativeSourceHash { get; }
    public string SourceRevision { get; }
    public LegacySkillParameters? SkillParameters { get; }

    private LegacyCombatCatalog(LegacyVeterancyLevel[] veterancy, ProgressionRules rank, ProgressionRules title,
        int[] health, int[] healing, int[] training, CombatContextRules context, string nativeHash, string revision, LegacySkillParameters? skillParameters)
    {
        Veterancy = Array.AsReadOnly(veterancy); RankProgression = rank; TitleProgression = title;
        RankHealth = Numbered(health); TitleHealing = Numbered(healing); Context = context;
        NativeSourceHash = nativeHash; SourceRevision = revision;
        SkillParameters = skillParameters;
        UnitProgression = new() { Levels = Array.AsReadOnly(veterancy.Select((v, i) => new ProgressionLevel(Id(i),
            i == veterancy.Length - 1 ? null : veterancy[i + 1].ExperienceCost, v.PromotionHealing)).ToArray()),
            SingleAdvancePerAward = true, AccumulateAtCap = true, DiscardExperienceOnReachingCap = false };
        Training = new() { Stages = Array.AsReadOnly(veterancy.Select((v, i) => new TrainingStage(Id(i),
            i == veterancy.Length - 1 ? null : training[i], v.PromotionHealing)).ToArray()) };
        Training.Validate();
    }

    private static string Id(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static IReadOnlyDictionary<string, int> Numbered(IEnumerable<int> values) =>
        new ReadOnlyDictionary<string, int>(values.Select((v, i) => (v, i)).ToDictionary(p => Id(p.i), p => p.v, StringComparer.Ordinal));

    public void VerifyNativeSource(ReadOnlySpan<byte> source)
    {
        if (!string.Equals(Convert.ToHexStringLower(SHA256.HashData(source)), NativeSourceHash, StringComparison.Ordinal))
            throw new RuleViolation("legacy.native_source_changed", "Native tables differ from their preserved source");
    }

    public static LegacyCombatCatalog Read(string tablesJson, string terrainXml, string installationXml, LegacyBuildingCatalog buildings,
        LegacySkillParameters? skillParameters = null)
    {
        using var document = JsonDocument.Parse(tablesJson);
        var tables = document.RootElement;
        if (tables.GetProperty("schema_version").GetInt32() != 1)
            throw new RuleViolation("legacy.combat_catalog", "Unsupported native rule table version");
        string nativeHash = tables.GetProperty("source_sha256").GetString() ?? "";
        if (nativeHash.Length != 64 || nativeHash.Any(c => !Uri.IsHexDigit(c)))
            throw new RuleViolation("legacy.combat_catalog", "Invalid native source digest");
        int[] Values(string key) => tables.GetProperty(key).EnumerateArray().Select(v => v.GetInt32()).ToArray();
        var rows = tables.GetProperty("unit_veterancy_rows").EnumerateArray().Select(v => v.EnumerateArray().Select(n => n.GetInt32()).ToArray()).ToArray();
        if (rows.Length != 6 || rows.Any(r => r.Length != 4 || r.Any(v => v < 0)) || rows.Skip(1).Any(r => r[3] == 0))
            throw new RuleViolation("legacy.combat_catalog", "Invalid original veterancy rows");
        var veterancy = rows.Select(r => new LegacyVeterancyLevel(r[0], r[1], r[2], r[3])).ToArray();
        ProgressionRules Track(string key)
        {
            int[] costs = Values(key);
            return new() { Levels = Array.AsReadOnly(costs.Select((cost, i) => new ProgressionLevel(Id(i), cost))
                .Append(new(Id(costs.Length), null)).ToArray()) };
        }
        var rank = Track("commander_rank_xp"); var title = Track("commander_nobility_xp");
        int[] health = Values("commander_rank_health_bonus"), healing = Values("commander_nobility_healing");
        int[] training = Values("training_money");
        if (health.Length != rank.Levels.Count || healing.Length != title.Levels.Count || health.Any(v => v < 0) || healing.Any(v => v < 0))
            throw new RuleViolation("legacy.combat_catalog", "Commander progression and bonus rows disagree");
        if (training.Length != veterancy.Length || training.Any(v => v < 0))
            throw new RuleViolation("legacy.combat_catalog", "Training costs have no corresponding veterancy row");
        var context = new CombatContextRules { Buildings = buildings.BuildingDefenseRules(), UseResolvedFallback = false,
            TerrainDefense = Defense(terrainXml, "terraintypes", "terraintype"),
            InstallationDefense = Defense(installationXml, "installations", "installation"),
            VeterancyArmor = Numbered(veterancy.Select(v => v.Armor)) };
        context.Validate(); Progression.Award(new(0, 0), 0, rank); Progression.Award(new(0, 0), 0, title);
        string revision = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new[] { tablesJson, terrainXml, installationXml, buildings.SourceRevision })));
        if (skillParameters != null)
            revision = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { revision, skillParameters.Skills })));
        return new(veterancy, rank, title, health, healing, training, context, nativeHash.ToLowerInvariant(), revision, skillParameters);
    }

    private static IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> Defense(string xml, string rootName, string elementName)
    {
        var root = XDocument.Parse(xml).Root;
        if (root?.Name != rootName) throw new RuleViolation("legacy.combat_catalog", "Invalid defense definition root");
        var result = new Dictionary<string, IReadOnlyDictionary<string, int>>(StringComparer.Ordinal);
        foreach (var row in root.Elements(elementName))
        {
            string kind = (string?)row.Attribute("type") ?? "";
            int infantry = (int?)row.Attribute("penalty_infantry") ?? 0;
            int cavalry = (int?)row.Attribute("penalty_cavalry") ?? 0;
            int artillery = (int?)row.Attribute("penalty_artillery") ?? 0;
            // The native lookup maps naval/fort attackers to the artillery column too.
            var values = new Dictionary<string, int> { ["infantry"] = infantry, ["cavalry"] = cavalry,
                ["artillery"] = artillery, ["naval"] = artillery, ["fort"] = artillery };
            if (string.IsNullOrWhiteSpace(kind) || !result.TryAdd(kind, new ReadOnlyDictionary<string, int>(values)))
                throw new RuleViolation("legacy.combat_catalog", "Invalid or duplicate defense kind");
        }
        return new ReadOnlyDictionary<string, IReadOnlyDictionary<string, int>>(result);
    }

    public WorldBattleRules BattleRules() => new() { Combat = new() { LegacySkills = SkillParameters }, Context = Context, UnitProgression = UnitProgression,
        RankProgression = RankProgression, TitleProgression = TitleProgression, RankHealthBonuses = RankHealth, TitleHealing = TitleHealing };

    public WorldTurnRules TurnRules(WorldTurnRules buildings) => buildings with { VeterancyHealing = Numbered(Veterancy.Select(v => v.TurnHealing)) };
}
