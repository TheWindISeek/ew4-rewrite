using System.Text.Json;
using Ew4.Core;

internal static class CommanderChecks
{
    public static int Run(string root)
    {
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "reverse/reports/skill_vectors.json")));
        string[] categories = ["infantry", "cavalry", "artillery", "naval", "fort"];
        int count = 0;
        foreach (var vector in data.RootElement.EnumerateArray())
        {
            int skill = vector.GetProperty("skill").GetInt32(), effect = vector.GetProperty("effect").GetInt32();
            int target = vector.GetProperty("target").GetInt32(), category = vector.GetProperty("category").GetInt32();
            var commander = new Commander
            {
                Id = "test", Movement = 3, Skills = skill < 0 ? [] : [Ew4Combat.Skill(skill)],
                Items = effect < 0 ? [] : [new("i0", Ew4Commander.Effect(effect), 2, target < 0 ? null : categories[target]),
                    new("i1", Ew4Commander.Effect(effect), 4, target < 0 ? null : categories[target])]
            };
            var actual = Ew4Commander.Resolve(commander, categories[category]);
            var expected = vector.GetProperty("expected");
            int Int(string k) => expected.GetProperty(k).GetInt32();
            bool Flag(string k) => expected.GetProperty(k).GetBoolean();
            var wanted = new CommanderEffects(Int("minimum_attack_bonus"), Int("maximum_attack_bonus"), Int("movement_bonus"),
                Int("attack_equipment"), Int("defense_equipment"), Int("field_healing"), Int("adjacent_attack"), Int("adjacent_defense"),
                Int("adjacent_healing"), Flag("geography"), Flag("sea_immunity"), Flag("full_attack_rolls"), Int("food_consumption") == 0,
                expected.GetProperty("economic_multiplier").GetSingle());
            if (actual != wanted) throw new InvalidOperationException($"Commander vector {count}: {actual} != {wanted}");
            count++;
        }
        var many = new Commander { Id = "new.commander", DisplayName = "中文将领", Skills = Enumerable.Range(0, 100).Select(Ew4Combat.Skill).ToArray() };
        for (int i = 0; i < 10; i++) many = Ew4Commander.Equip(many, new Equipment("item." + i, Ew4Commander.Effect(9), 1), 10);
        if (many.Items.Count != 10 || Ew4Commander.Resolve(many, "infantry").MovementBonus != 10)
            throw new InvalidOperationException("Unexpected fixed equipment/skill capacity");
        try
        {
            Ew4Commander.Equip(many, new Equipment("over", Ew4Commander.Effect(9), 1), 10);
            throw new InvalidOperationException("Configured equipment capacity was ignored");
        }
        catch (RuleViolation error) when (error.Code == "equipment.unavailable") { }
        return count + 2;
    }
}
