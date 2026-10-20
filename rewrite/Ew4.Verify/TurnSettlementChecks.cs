using System.Text.Json;
using Ew4.Core;

internal static class TurnSettlementChecks
{
    public static int Run(string root)
    {
        int checks = 0;
        void Check(bool value, string label)
        {
            checks++;
            if (!value) throw new InvalidOperationException(label);
        }
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "reverse/reports/turn_settlement_vectors.json")));
        foreach (var vector in data.RootElement.GetProperty("end_cell").EnumerateArray())
        {
            int Int(string name) => vector.GetProperty(name).GetInt32();
            bool Flag(string name) => vector.GetProperty(name).GetBoolean();
            var input = new SiteEndInput
            {
                BuildingKind = vector.GetProperty("building").ValueKind == JsonValueKind.Null ? null : Int("building") == 4 ? "farmland" : "city",
                BuildingLevel = Int("level"), BuildingHealing = Int("building_heal"), Environment = Int("environment"),
                Units = Flag("unit") ? [new() { Id = "unit", Health = Int("health"), MaximumHealth = Int("maximum"),
                    ConstructionTurns = Int("construction"), Acted = Flag("acted"), ForceRecovery = Flag("force"),
                    Shortage = Int("shortage"), HasCommander = Flag("general"),
                    VeterancyHealing = Flag("veterancy_row") ? Int("veterancy_heal") : 0,
                    FieldHealing = Int("field_heal"), AdjacentHealing = Int("adjacent_heal"),
                    TitleHealing = Int("title_heal"), Burning = Int("burning") }] : []
            };
            var rng = new Ew4Random(vector.GetProperty("seed").GetUInt64());
            var result = TurnSettlement.EndSite(input, rng);
            var expected = vector.GetProperty("expected");
            Check(result.BuildingLevel == expected.GetProperty("level").GetInt32()
                && result.Environment == expected.GetProperty("environment").GetInt32()
                && result.RandomState == expected.GetProperty("seed").GetUInt64() && rng.State == result.RandomState,
                "native end-turn site/farmland/decay/RNG ordering");
            if (Flag("unit")) Check(result.Units.Single() == new UnitEndResult("unit", expected.GetProperty("health").GetInt32(),
                expected.GetProperty("burning").GetInt32(), expected.GetProperty("force").GetBoolean()), "native end-turn healing and damage");
        }
        foreach (var vector in data.RootElement.GetProperty("income").EnumerateArray())
        {
            var sites = vector.GetProperty("tiles").EnumerateArray().Select(v =>
            {
                var baseIncome = v.GetProperty("base");
                bool building = v.GetProperty("building").ValueKind != JsonValueKind.Null;
                bool row = v.GetProperty("row").GetBoolean() && building;
                long money = row ? baseIncome[0].GetInt64() : 0;
                if (building && v.GetProperty("building").GetInt32() == 0 && v.GetProperty("city_row").GetBoolean())
                    money = v.GetProperty("city_money").GetInt64();
                return new ProductionSite(new(money, row ? baseIncome[1].GetInt64() : 0, row ? baseIncome[2].GetInt64() : 0),
                    v.GetProperty("general").GetBoolean() ? v.GetProperty("multiplier").GetSingle() : null);
            }).ToArray();
            var tech = vector.GetProperty("technology");
            long Bonus(int i) => tech[i].GetInt32() is >= 1 and <= 3 ? tech[i].GetInt32() * (i == 0 ? 20 : 10) : 0;
            var actual = EconomyProduction.Calculate(sites, vector.GetProperty("multiplier").GetSingle(), new(Bonus(0), Bonus(1), Bonus(2)));
            var expected = vector.GetProperty("expected");
            Check(actual == new Resources(expected[0].GetInt64(), expected[1].GetInt64(), expected[2].GetInt64()), "native per-city truncation and faction income");
        }
        Check(EconomyProduction.Calculate([new(new(20000, 12000, 30000))], 1, new(0, 0, 0)) == new Resources(20000, 12000, 30000), "no fixed resource cap");
        var random = new Ew4Random(100);
        try
        {
            TurnSettlement.EndSite(new() { Environment = 3, Units = [new() { Id = "bad", Health = 0, MaximumHealth = 100 }] }, random);
            throw new InvalidOperationException("Missing settlement rejection");
        }
        catch (RuleViolation error) { Check(error.Code == "settlement.unit" && random.State == 100, "invalid settlement retains RNG"); }
        var stacks = TurnSettlement.EndSite(new() { Environment = 3, Units = [new() { Id = "a", Health = 2, MaximumHealth = 100 },
            new() { Id = "b", Health = 5, MaximumHealth = 100 }] }, random);
        Check(stacks.Units.Count == 2 && stacks.Units.All(u => u.Health == 1), "all configured occupants receive environment damage");
        return checks;
    }
}
