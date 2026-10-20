using System.Xml.Linq;
using Ew4.Core;
using Ew4.Legacy;

namespace Ew4.Client;

public sealed record ScenarioChoice(string Id, string Name, int Mode);
public sealed record CampaignChapter(int Id, string Name, string Start, string End, string[] Countries);
public sealed record ScenarioPresentation(string Id, string Description, string? Commander, string Age,
    int Chapter, string? Alternate, string[] Countries, bool Hidden = false);

public sealed class ClientContent
{
    private readonly Func<string, byte[]> read;
    public LegacyScenarioCatalogs Catalogs { get; }
    public ExtensionLoader Extensions { get; }
    public IReadOnlyDictionary<int, LegacyTerrainMap> Maps { get; }
    public IReadOnlyDictionary<int, string> MapNames { get; }
    public IReadOnlyDictionary<int, string> Cities { get; }
    public IReadOnlyDictionary<string, string> Strings { get; }
    public IReadOnlyList<ScenarioChoice> Scenarios { get; }
    public IReadOnlyList<CampaignChapter> Chapters { get; }
    public IReadOnlyDictionary<string, ScenarioPresentation> Presentations { get; }
    public IReadOnlyList<XElement> ItemDefinitions { get; }
    public IReadOnlyList<XElement> PrincessDefinitions { get; }
    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<int, string>> scenarioCityNames;

    public ClientContent(Func<string, byte[]> read, IEnumerable<string> battleFiles)
    {
        this.read = read;
        string Text(string name)
        {
            using var reader = new StreamReader(new MemoryStream(read("original/" + name)), System.Text.Encoding.UTF8, true);
            return reader.ReadToEnd();
        }
        Extensions = ExtensionLoader.Load(read);
        var runtime = LegacyRuntimeCatalog.FromJson(Extensions.PatchCatalog(System.Text.Encoding.UTF8.GetString(read("data/catalog.json"))));
        runtime.VerifySources(name => read("original/" + name));
        var buildings = LegacyBuildingCatalog.Read(Text("def_construction.xml"), Text("def_capital.xml"));
        var combat = LegacyCombatCatalog.Read(System.Text.Encoding.UTF8.GetString(read("data/runtime_rule_tables.json")),
            Text("def_terraintype.xml"), Text("def_installation.xml"), buildings);
        var accounts = LegacyCommanderAccounts.Read(runtime, combat,
            System.Text.Encoding.UTF8.GetString(read("data/metagame_rule_tables.json")));
        Catalogs = new(runtime, LegacyTerrainRules.FromXml(Text("def_mapterrain.xml"), Text("def_terraintype.xml")), buildings,
            LegacyRecruitmentCatalog.Read(Text("def_card.xml")), combat, accounts, LegacyItemTradingCatalog.Read(Text("def_item.xml")),
            LegacyWarzoneTechnology.Read(Text("def_warzonetech.xml")), LegacyDialogueCatalog.Read(Text("def_dialogues.xml"), Text("stringtable_cn.ini")));
        var definitions = XDocument.Parse(Text("def_map.xml")).Root!.Elements("map").ToArray();
        Maps = definitions.ToDictionary(e => (int)e.Attribute("id")!, e => LegacyTerrainMap.Read(read("original/" + (string)e.Attribute("file")!)));
        MapNames = definitions.ToDictionary(e => (int)e.Attribute("id")!, e => (string)e.Attribute("name")!);
        Cities = XDocument.Parse(Text("def_areaname.xml")).Root!.Elements("areaname")
            .ToDictionary(e => (int)e.Attribute("id")!, e => (string)e.Attribute("name")!);
        var strings = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string line in Text("stringtable_cn.ini").Split('\n'))
        {
            int separator = line.IndexOf('=');
            if (separator > 0 && !line.TrimStart().StartsWith(';')) strings[line[..separator].Trim()] = line[(separator + 1)..].Trim();
        }
        Strings = strings;
        ItemDefinitions = XDocument.Parse(Text("def_item.xml")).Root!.Elements("item").ToArray();
        PrincessDefinitions = XDocument.Parse(Text("def_princess.xml")).Root!.Elements("Princess").ToArray();
        var battleEntries = XDocument.Parse(Text("def_battlelist.xml")).Root!.Elements("battle").ToArray();
        var namesByScenario = new Dictionary<string, IReadOnlyDictionary<int, string>>();
        foreach (var entry in battleEntries.Where(e => e.Attribute("citynames") != null))
        {
            string file = (string)entry.Attribute("citynames")!;
            if (file.Contains('/') || file.Contains('\\') || file.Contains(':') || Path.GetExtension(file) != ".xml")
                throw new RuleViolation("client.city_names", "City name files must be sibling XML resources");
            var names = ScenarioCityNames.Read(Text(file), Cities);
            namesByScenario.Add(Path.GetFileNameWithoutExtension((string)entry.Attribute("file")!), names);
            if (entry.Attribute("file2") is XAttribute alternate)
                namesByScenario.Add(Path.GetFileNameWithoutExtension(alternate.Value), names);
        }
        scenarioCityNames = namesByScenario;
        var battleNames = battleEntries
            .ToDictionary(e => Path.GetFileNameWithoutExtension((string)e.Attribute("file")!), e => (string)e.Attribute("name")!);
        Presentations = battleEntries.Select(e =>
        {
            string id = Path.GetFileNameWithoutExtension((string)e.Attribute("file")!);
            string key = (string)e.Attribute("name")!;
            int chapter = id.StartsWith("campaign", StringComparison.Ordinal) ? int.Parse(id[8..id.IndexOf('_')]) : 0;
            return new ScenarioPresentation(id, Label("desc_" + key), (string?)e.Attribute("commander"),
                (string?)e.Attribute("age") ?? "", chapter, e.Attribute("file2") == null ? null : Path.GetFileNameWithoutExtension((string)e.Attribute("file2")!),
                ((string?)e.Attribute("countries") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries), (string?)e.Attribute("hide") == "1");
        }).ToDictionary(p => p.Id);
        Chapters = XDocument.Parse(Text("def_battleline.xml")).Root!.Elements("line").Select((e, i) => new CampaignChapter(i + 1,
            Label((string)e.Attribute("name")!), (string)e.Attribute("start")!, (string)e.Attribute("end")!,
            ((string)e.Attribute("countries")!).Split(','))).ToArray();
        foreach (var presentation in Presentations.Values.Where(p => p.Alternate != null))
            battleNames[presentation.Alternate!] = battleNames[presentation.Id];
        string ScenarioName(string id) => id.StartsWith("conquest", StringComparison.Ordinal) ? Label("text_conquest_age_" + id[8..])
            : id == "tutorials1" ? Label("btn_basic") : id == "tutorials2" ? Label("btn_classic")
            : battleNames.TryGetValue(id, out string? key) ? Label("title_" + key) : id;
        Scenarios = battleFiles.Select(Path.GetFileNameWithoutExtension).Where(n => n != null)
            .Select(n => new ScenarioChoice(n!, ScenarioName(n!), n!.StartsWith("conquest", StringComparison.Ordinal) ? 2
                : n.StartsWith("tutorial", StringComparison.Ordinal) ? 4 : n.StartsWith("multiplay", StringComparison.Ordinal) ? 3 : 1)).OrderBy(s => s.Mode).ThenBy(s => s.Id, StringComparer.Ordinal).ToArray();
        if (Extensions.Victories.Keys.Any(id => !Scenarios.Any(s => s.Id == id)))
            throw new RuleViolation("extension.victory_scenario", "Victory rules reference an unknown scenario");
    }

    public string Label(string key) => Strings.TryGetValue(key, out string? label) ? label : Strings.TryGetValue("name_" + key, out label) ? label : key;
    public LegacyBattle Battle(string id)
    {
        if (!Scenarios.Any(s => s.Id == id)) throw new RuleViolation("client.scenario", "Unknown scenario");
        return LegacyBattle.Read(read("original/" + id + ".btl"));
    }
    public LegacyScenarioStart Start(string id, string? country, PlayerAccount? account = null)
    {
        var choice = Scenarios.Single(s => s.Id == id);
        if (choice.Mode == 3) throw new RuleViolation("client.mode", "Multiplayer requires a multiplayer session host");
        account ??= new PlayerAccount { Id = "local", ContentRevision = Catalogs.Accounts.SourceRevision, Medals = 20_000_000 };
        var battle = Battle(id);
        if (battle.Jurisdictions != null && choice.Mode is not (1 or 2))
            throw new RuleViolation("client.jurisdiction_mode", "手工辖区仅支持战役与征服剧本");
        var started = LegacyScenarioHost.Start(battle, Maps, Catalogs, new LegacyScenarioRequest
        {
            Id = id, Mode = choice.Mode, SelectedCountry = country,
            CityNames = scenarioCityNames.TryGetValue(id, out var names) ? names : Cities,
            Warzone = Presentations.Values.FirstOrDefault(p => p.Id == id || p.Alternate == id)?.Chapter is int chapter && chapter > 0 ? chapter - 1 : 0,
            CountryNames = battle.Factions.Select(f => f.CountryId).Distinct().ToDictionary(c => c, Label),
            CombatSeed = choice.Mode == 4 ? 100 : (ulong)Random.Shared.NextInt64(1, long.MaxValue), AiSeed = (ulong)Random.Shared.NextInt64(1, long.MaxValue)
        }, account);
        // Apply playable rules after import so existing scenario/save fingerprints stay compatible.
        started = started with { Rules = started.Rules with { Battle = started.Rules.Battle with {
            Combat = started.Rules.Battle.Combat with { LethalStrikePreventsCounterattack = true } } } };
        if (choice.Mode is 1 or 2)
        {
            var world = started.Session.World;
            started = started with { Session = started.Session with { World = world with {
                Jurisdictions = battle.Jurisdictions?.Apply(world) ?? TerritorialJurisdictions.Create(world) } } };
        }
        var customVictory = battle.Victory ?? Extensions.Victories.GetValueOrDefault(id);
        if (Extensions.Enabled || customVictory != null)
        {
            var rules = started.Rules;
            var victory = rules.Turns.Victory with { Custom = customVictory, Scripts = Extensions.Bus.Scripts };
            if (customVictory != null)
            {
                ValidateVictoryTargets(customVictory, started.Session.World);
                Extensions.ValidateVictoryScripts(customVictory);
            }
            var movement = rules.Movement with { Extensions = Extensions.Bus, Victory = victory };
            var turns = rules.Turns with { Extensions = Extensions.Bus, Victory = victory };
            var combat = rules.Battle with { Combat = rules.Battle.Combat with { Extensions = Extensions.Bus }, Victory = victory };
            started = started with { Rules = rules with { Movement = movement, Turns = turns, Battle = combat,
                Ai = rules.Ai with { Movement = movement } } };
            if (Extensions.Enabled)
            {
                var world = started.Session.World;
                string revision = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(world.ContentRevision + ":" + Extensions.Revision)));
                world = world with { ContentRevision = revision };
                foreach (string unit in world.Units.Select(u => u.Id).ToArray())
                    world = Extensions.Bus.Dispatch(world, "scenario.start", unit, new Ew4Random(world.RandomState));
                world.Validate();
                started = started with { Session = started.Session with { World = world } };
            }
            if (customVictory != null)
            {
                var world = started.Session.World;
                var outcome = WorldVictory.Evaluate(world, turns.PlayerCamp!, victory, turns.NeutralCamps);
                if (outcome != BattleOutcome.Continue)
                    started = started with { Session = started.Session with { World = world with { Outcome = outcome, Phase = TurnPhase.Finished } } };
            }
        }
        return started;
    }
    private static void ValidateVictoryTargets(ExtensionVictoryRules rules, WorldState world)
    {
        void Check(VictoryCondition c)
        {
            if (c.Cells.Any(i => i < 0 || i >= world.Cells.Count || !world.Cells[i].Terrain.Active)
                || c.Factions.Any(id => world.Factions.All(f => f.Id != id)) || c.Units.Any(id => world.Units.All(u => u.Id != id)))
                throw new RuleViolation("extension.victory_reference", "Victory condition references an unknown initial unit/faction/cell");
            foreach (var child in c.Conditions) Check(child);
        }
        Check(rules.Win); if (rules.Lose != null) Check(rules.Lose);
    }
}
