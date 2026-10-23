using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Xml.Linq;
using Ew4.Platform.Engine;

namespace Ew4.Platform.Content.Original;

public sealed record OriginalDialogue(string Id, int Commander, bool Left, int TextId, string Text);

public sealed class OriginalDialogueCatalog
{
    public IReadOnlyDictionary<int, OriginalDialogue> Definitions { get; }
    public string SourceRevision { get; }
    private OriginalDialogueCatalog(Dictionary<int, OriginalDialogue> definitions, string revision)
    { Definitions = new ReadOnlyDictionary<int, OriginalDialogue>(definitions); SourceRevision = revision; }

    public static OriginalDialogueCatalog Read(string xml, string stringTable)
    {
        // Read only dialogue keys: unrelated legacy UI keys can be duplicated.
        var texts = new Dictionary<string, string>(StringComparer.Ordinal);
        using var reader = new StringReader(stringTable);
        while (reader.ReadLine() is string line)
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith(';') || line.TrimStart().StartsWith('\uFF1B')) continue;
            if (!line.TrimStart().StartsWith("desc_dialogue ", StringComparison.Ordinal)) continue;
            int separator = line.IndexOf('=');
            if (separator < 0) throw new RuleViolation("legacy.stringtable", "Expected a literal key=value line");
            string key = line[..separator].Trim();
            if (key.Length == 0 || !texts.TryAdd(key, line[(separator + 1)..]))
                throw new RuleViolation("legacy.stringtable", "Empty or duplicate string-table key");
        }
        var document = XDocument.Parse(xml);
        if (document.Root?.Name != "dialogues") throw new RuleViolation("legacy.dialogues", "Expected dialogue definitions");
        var definitions = new Dictionary<int, OriginalDialogue>();
        foreach (var element in document.Root.Elements("dialogue"))
        {
            int id = (int?)element.Attribute("id") ?? throw new RuleViolation("legacy.dialogues", "Missing dialogue ID");
            int commander = (int?)element.Attribute("commander") ?? throw new RuleViolation("legacy.dialogues", "Missing dialogue portrait");
            int text = (int?)element.Attribute("textid") ?? throw new RuleViolation("legacy.dialogues", "Missing dialogue text ID");
            int left = (int?)element.Attribute("left") ?? 0;
            if (id < 0 || commander < 0 || left is not (0 or 1))
            {
                throw new RuleViolation("legacy.dialogues", "Invalid or duplicate dialogue or missing text");
            }

            if (!texts.TryGetValue("desc_dialogue " + text.ToString(CultureInfo.InvariantCulture), out string? value))
            {
                throw new RuleViolation("legacy.dialogues", "Invalid or duplicate dialogue or missing text");
            }

            if (!definitions.TryAdd(id, new("legacy.dialogue." + id.ToString(CultureInfo.InvariantCulture), commander, left == 1, text, value)))
            {
                continue;
            }
        }
        string revision = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new[] { xml, stringTable })));
        return new(definitions, revision);
    }
}

public sealed record OriginalMissingEvent(string Kind, string Subject, int EventId);
public sealed record OriginalEventImport(IReadOnlyList<ScenarioEvent> Events, IReadOnlyList<string> TriggeredEvents,
    IReadOnlyDictionary<string, OriginalDialogue> Dialogues, IReadOnlyList<OriginalMissingEvent> MissingReferences,
    int DeathBindings, int CaptureBindings, string SourceRevision);

public static class OriginalScenarioEvents
{
    public static OriginalEventImport Import(OriginalBattle battle, IReadOnlyList<OriginalUnitBinding> units,
        IReadOnlyList<CellState> cells, IReadOnlyList<string> factionIds, OriginalDialogueCatalog dialogues)
    {
        if (factionIds.Count != battle.Factions.Count || factionIds.Any(string.IsNullOrWhiteSpace)
            || factionIds.Distinct(StringComparer.Ordinal).Count() != factionIds.Count || units.Count != battle.Armies.Count
            || units.Select(u => u.Unit.Id).Distinct(StringComparer.Ordinal).Count() != units.Count
            || units.Where((u, i) => u.Source != battle.Armies[i]).Any())
            throw new RuleViolation("legacy.event_context", "Event import needs the matching ordered unit and faction bindings");
        // 8d6c0 replaces a duplicate's payload, retaining its first cell, state and position.
        var effectiveEvents = battle.Events.GroupBy(e => e.Id).Select(g => g.Last() with
            { Cell = g.First().Cell, Triggered = g.First().Triggered }).ToArray();
        var countries = new Dictionary<string, string>(StringComparer.Ordinal);
        for (int i = 0; i < factionIds.Count; i++)
            // Native Country lookup returns the first matching name; indexed factions stay distinct.
            countries.TryAdd(battle.Factions[i].CountryId, factionIds[i]);
        var rules = effectiveEvents.ToDictionary(e => e.Id, _ => new List<ScenarioEventRule>());
        var missing = new List<OriginalMissingEvent>();
        var definitions = effectiveEvents.ToDictionary(e => e.Id);
        var selected = new Dictionary<string, OriginalDialogue>(StringComparer.Ordinal);
        int deaths = 0, captures = 0;
        foreach (var ev in effectiveEvents)
        {
            // Native dispatch handles 0..3 and 5; other nonnegative effects still trigger dialogue.
            if (ev.Id < 0 || ev.Condition < 0 || ev.Condition > 2 || ev.Effect < 0
                || (ev.Condition == 2 && ev.Round < 0)
                || (ev.Effect == 5 && (ev.Cell < 0 || ev.Cell >= cells.Count || !cells[ev.Cell].Terrain.Active)))
                throw new RuleViolation("legacy.event_definition", $"Unsupported legacy event rule: id={ev.Id}, condition={ev.Condition}, effect={ev.Effect}, round={ev.Round}, cell={ev.Cell}");
            if (dialogues.Definitions.TryGetValue(ev.Dialogue, out var dialogue)) selected[dialogue.Id] = dialogue;
            if (ev.Condition == 2) rules[ev.Id].Add(Rule(ev, TriggerKind.Round));
        }
        foreach (var unit in units.Where(u => u.Source.State70 >= 0))
        {
            int reference = unit.Source.State70;
            if (!definitions.TryGetValue(reference, out var ev)) { missing.Add(new("death", unit.Unit.Id, reference)); continue; }
            rules[ev.Id].Add(Rule(ev, TriggerKind.UnitDestroyed) with { UnitId = unit.Unit.Id, RequireCommander = true });
            deaths++;
        }
        foreach (var site in battle.EffectiveSites.Where(s => s.State11 >= 0))
        {
            if (site.Cell < 0 || site.Cell >= cells.Count || !cells[site.Cell].Terrain.Active)
                throw new RuleViolation("legacy.event_context", "Capture reference is outside the active map");
            int reference = site.State11;
            if (!definitions.TryGetValue(reference, out var ev))
            { missing.Add(new("capture", site.Cell.ToString(CultureInfo.InvariantCulture), reference)); continue; }
            rules[ev.Id].Add(Rule(ev, TriggerKind.CellCaptured) with { TriggerCell = site.Cell, RequireDifferentCamps = true });
            captures++;
        }
        var events = effectiveEvents.Select(ev => new ScenarioEvent { Id = Id(ev.Id), Rules = rules[ev.Id].AsReadOnly(),
            DialogueId = dialogues.Definitions.TryGetValue(ev.Dialogue, out var dialogue) ? dialogue.Id : null }).ToArray();
        var triggered = effectiveEvents.Where(e => e.Triggered).Select(e => Id(e.Id)).ToArray();
        string revision = Convert.ToHexStringLower(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
            { battle = Convert.ToBase64String(battle.ToBytes()), events, dialogueRevision = dialogues.SourceRevision })));
        return new(Array.AsReadOnly(events), Array.AsReadOnly(triggered), new ReadOnlyDictionary<string, OriginalDialogue>(selected),
            missing.AsReadOnly(), deaths, captures, revision);

        ScenarioEventRule Rule(OriginalEvent ev, TriggerKind trigger)
        {
            bool morale = ev.Effect < (trigger == TriggerKind.Round ? 3 : 4);
            string? faction = null;
            var target = EventFactionTarget.Explicit;
            if (trigger == TriggerKind.CellCaptured) target = ev.Effect == 0 ? EventFactionTarget.NewOwner : EventFactionTarget.PreviousOwner;
            else if (!countries.TryGetValue(ev.Country, out faction) && trigger == TriggerKind.UnitDestroyed) target = EventFactionTarget.PreviousOwner;
            return new() { Trigger = trigger, Round = ev.Round,
                Effect = morale ? EventEffectKind.SetMorale : ev.Effect == 5 ? EventEffectKind.EnvironmentSpread : EventEffectKind.None,
                Value = morale ? new[] { 1, -1, -2, -3 }[ev.Effect] : 0, FactionId = faction, Target = target, Cell = ev.Cell };
        }
    }

    private static string Id(int value) => "legacy.event." + value.ToString(CultureInfo.InvariantCulture);
}
