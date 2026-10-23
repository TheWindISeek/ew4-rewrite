using System.Xml.Linq;
using Ew4.Platform.Engine;
using Ew4.Platform.Content.Build;

namespace Ew4.Service;

public sealed record ScenarioChoice(string Id, string Name, int Mode);
public sealed record CampaignChapter(int Id, string Name, string Start, string End, string[] Countries);
public sealed record ScenarioPresentation(string Id, string Description, string? Commander, string Age,
    int Chapter, string? Alternate, string[] Countries, bool Hidden = false, string? StrategicName = null,
    int? CenterX = null, int? CenterY = null);

public sealed partial class ClientContent
{
    private readonly Func<string, byte[]> read;
    public OriginalScenarioCatalogs Catalogs { get; }
    public ExtensionLoader Extensions { get; }
    public CountryCatalog Countries { get; }
    public IReadOnlyDictionary<int, OriginalTerrainMap> Maps { get; }
    public IReadOnlyDictionary<int, string> MapNames { get; }
    public IReadOnlyDictionary<int, string> Cities { get; }
    public IReadOnlyDictionary<string, string> Strings { get; }
    public IReadOnlyList<ScenarioChoice> Scenarios { get; }
    public IReadOnlyList<CampaignChapter> Chapters { get; }
    public IReadOnlyDictionary<string, ScenarioPresentation> Presentations { get; }
    public IReadOnlyList<XElement> ItemDefinitions { get; }
    public IReadOnlyList<XElement> PrincessDefinitions { get; }
    public bool CorrectOriginalEquipment { get; }
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<int, string>> scenarioCityNames;
    private readonly IReadOnlyDictionary<string, string> scenarioDisplayNames;

    public ClientContent(
        Func<string, byte[]> read,
        IEnumerable<string> battleFiles,
        IReadOnlyDictionary<string, string>? scenarioDisplayNames = null,
        bool correctOriginalEquipment = true,
        bool verifySourceHashes = true)
    {
        this.read = read;
        this.scenarioDisplayNames = scenarioDisplayNames ?? new Dictionary<string, string>(StringComparer.Ordinal);
        LegacyPackRuntimeLoadResult loaded = LegacyPackRuntimeLoader.Load(
            read,
            new LegacyPackRuntimeLoadOptions(correctOriginalEquipment, verifySourceHashes));
        Countries = loaded.Countries;
        Extensions = loaded.Extensions;
        Catalogs = loaded.Catalogs;
        Maps = loaded.Maps;
        MapNames = loaded.MapNames;
        Cities = loaded.Cities;
        Strings = loaded.Strings;
        CorrectOriginalEquipment = loaded.CorrectOriginalEquipment;
        scenarioCityNames = loaded.CityNamesByScenario;
        string Text(string name)
        {
            using var reader = new StreamReader(new MemoryStream(read("original/" + name)), System.Text.Encoding.UTF8, true);
            return reader.ReadToEnd();
        }
        ItemDefinitions = XDocument.Parse(Text("def_item.xml")).Root!.Elements("item").ToArray();
        PrincessDefinitions = XDocument.Parse(Text("def_princess.xml")).Root!.Elements("Princess").ToArray();
        var battleEntries = XDocument.Parse(Text("def_battlelist.xml")).Root!.Elements("battle").ToArray();
        var battleNames = battleEntries
            .ToDictionary(e => Path.GetFileNameWithoutExtension((string)e.Attribute("file")!), e => (string)e.Attribute("name")!);
        Presentations = battleEntries.Select(e =>
        {
            string id = Path.GetFileNameWithoutExtension((string)e.Attribute("file")!);
            string key = (string)e.Attribute("name")!;
            int chapter = id.StartsWith("campaign", StringComparison.Ordinal) ? int.Parse(id[8..id.IndexOf('_')]) : 0;
            int? centerX = int.TryParse((string?)e.Attribute("centerx"), out int parsedX) ? parsedX : null;
            int? centerY = int.TryParse((string?)e.Attribute("centery"), out int parsedY) ? parsedY : null;
            return new ScenarioPresentation(id, Label("desc_" + key), (string?)e.Attribute("commander"),
                (string?)e.Attribute("age") ?? "", chapter, e.Attribute("file2") == null ? null : Path.GetFileNameWithoutExtension((string)e.Attribute("file2")!),
                ((string?)e.Attribute("countries") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries), (string?)e.Attribute("hide") == "1",
                key, centerX, centerY);
        }).ToDictionary(p => p.Id);
        Chapters = XDocument.Parse(Text("def_battleline.xml")).Root!.Elements("line")
            .Select((e, i) => (Element: e, Index: i + 1))
            .Where(line => line.Element.Attribute("hide")?.Value != "1")
            .Select(line => new CampaignChapter(line.Index,
                Label((string)line.Element.Attribute("name")!), (string)line.Element.Attribute("start")!,
                (string)line.Element.Attribute("end")!,
                ((string)line.Element.Attribute("countries")!).Split(','))).ToArray();
        foreach (var presentation in Presentations.Values.Where(p => p.Alternate != null))
            battleNames[presentation.Alternate!] = battleNames[presentation.Id];
        string ScenarioName(string id)
        {
            if (this.scenarioDisplayNames.TryGetValue(id, out string? overlay) && !string.IsNullOrEmpty(overlay))
            {
                return overlay;
            }

            return id.StartsWith("conquest", StringComparison.Ordinal) ? Label("text_conquest_age_" + id[8..])
                : id == "tutorials1" ? Label("btn_basic") : id == "tutorials2" ? Label("btn_classic")
                : battleNames.TryGetValue(id, out string? key) ? Label("title_" + key) : id;
        }
        Scenarios = battleFiles.Select(Path.GetFileNameWithoutExtension).Where(n => n != null)
            .Select(n => new ScenarioChoice(n!, ScenarioName(n!), n!.StartsWith("conquest", StringComparison.Ordinal) ? 2
                : n.StartsWith("tutorial", StringComparison.Ordinal) ? 4 : n.StartsWith("multiplay", StringComparison.Ordinal) ? 3 : 1)).OrderBy(s => s.Mode).ThenBy(s => s.Id, StringComparer.Ordinal).ToArray();
        if (Extensions.Victories.Keys.Any(id => !Scenarios.Any(s => s.Id == id)))
            throw new RuleViolation("extension.victory_scenario", "Victory rules reference an unknown scenario");
        if (Extensions.VictoryPresentations.Keys.Any(id => id != "*" && !Scenarios.Any(s => s.Id == id)))
            throw new RuleViolation("extension.victory_presentation", "Victory screen references an unknown scenario");
        if (Extensions.ScenarioEvents.Keys.Any(id => !Scenarios.Any(s => s.Id == id)))
            throw new RuleViolation("scenario.reference", "Event rules reference an unknown scenario");
        if (Extensions.DeploymentLimits.Keys.Any(id => id != "*" && !Scenarios.Any(s => s.Id == id)))
            throw new RuleViolation("extension.deployment_limit", "Deployment limits reference an unknown scenario");
    }

    public string Label(string key) => Strings.TryGetValue(key, out string? label) ? label : Strings.TryGetValue("name_" + key, out label) ? label : key;
    public string CountryName(string id) => Countries.Get(id)?.Name ?? Label(id);
    public CountryDefinition? Country(string id) => Countries.Get(id);
    internal ClientContent WithoutEquipmentCorrections() =>
        new(read, Scenarios.Select(s => s.Id + ".btl"), scenarioDisplayNames, false);
    public OriginalBattle Battle(string id)
    {
        if (!Scenarios.Any(s => s.Id == id)) throw new RuleViolation("client.scenario", "Unknown scenario");
        var battle = OriginalBattle.Read(read("original/" + id + ".btl"));
        if (battle.CountryIds != null && battle.Factions.Any(f => Country(f.CountryId) == null))
            throw new RuleViolation("country.reference", "CNTR references an unregistered global country in " + id);
        return battle;
    }
}
