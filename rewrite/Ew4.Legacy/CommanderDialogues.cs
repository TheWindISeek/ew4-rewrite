using Ew4.Core;

namespace Ew4.Legacy;

public sealed record CommanderDialogueLines
{
    public string[] Kill { get; init; } = [];
    public string[] Occupy { get; init; } = [];
    public string[] Retreat { get; init; } = [];
    public CommanderKillRule[] KillRules { get; init; } = [];
}

public sealed record CommanderKillCondition
{
    public string[] VictimCommanders { get; init; } = [];
    public string[] VictimCategories { get; init; } = [];
    public bool? VictimHasCommander { get; init; }
    public string[] Scenarios { get; init; } = [];
    public int MinRound { get; init; } = 1;
    public int MaxRound { get; init; } = int.MaxValue;
    public int KillerHealthPercentAtMost { get; init; } = 100;
}

public sealed record CommanderKillRule
{
    public string Id { get; init; } = "";
    public CommanderKillCondition When { get; init; } = new();
    public int Chance { get; init; } = 100;
    public string Selection { get; init; } = "random";
    public string[] Lines { get; init; } = [];
}

internal sealed record CommanderDialogueFile(int Schema, Dictionary<string, CommanderDialogueLines> Commanders)
{
    public void Validate()
    {
        if (Schema != 1 || Commanders == null)
            throw new RuleViolation("extension.commander_dialogues", "Expected schema 1 and a commanders object");
        foreach (var entry in Commanders)
        {
            if (string.IsNullOrWhiteSpace(entry.Key) || entry.Value == null)
                throw new RuleViolation("extension.commander_dialogues", "Missing commander ID or dialogue entry");
            foreach (var lines in new[] { entry.Value.Kill, entry.Value.Occupy, entry.Value.Retreat })
                if (!ValidLines(lines))
                    throw new RuleViolation("extension.commander_dialogues", "Expected up to 128 nonempty lines of at most 2000 characters: " + entry.Key);
            var rules = entry.Value.KillRules;
            if (rules == null || rules.Length > 128)
                throw new RuleViolation("extension.commander_dialogues", "Expected up to 128 kill rules: " + entry.Key);
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var rule in rules)
            {
                if (rule == null || string.IsNullOrWhiteSpace(rule.Id) || rule.Id.Length > 128 || !ids.Add(rule.Id)
                    || rule.When == null || rule.Chance is < 0 or > 100 || rule.Selection is not ("first" or "random")
                    || !ValidLines(rule.Lines) || rule.Lines.Length == 0)
                    throw new RuleViolation("extension.commander_dialogues", "Invalid or duplicate kill rule: " + entry.Key);
                var condition = rule.When;
                if (!ValidIds(condition.VictimCommanders) || !ValidIds(condition.Scenarios) || !ValidIds(condition.VictimCategories)
                    || condition.VictimCategories.Any(c => c is not ("infantry" or "cavalry" or "artillery" or "naval" or "fort"))
                    || condition.MinRound < 1 || condition.MaxRound < condition.MinRound || condition.KillerHealthPercentAtMost is < 0 or > 100
                    || condition.VictimHasCommander == false && condition.VictimCommanders.Length > 0)
                    throw new RuleViolation("extension.commander_dialogues", "Invalid kill conditions: " + rule.Id);
            }
        }
    }
    private static bool ValidLines(string[]? lines) => lines != null && lines.Length <= 128
        && lines.All(line => !string.IsNullOrWhiteSpace(line) && line.Length <= 2000);
    private static bool ValidIds(string[]? values) => values != null && values.Length <= 128
        && values.All(value => !string.IsNullOrWhiteSpace(value) && value.Length <= 128)
        && values.Distinct(StringComparer.Ordinal).Count() == values.Length;
}
