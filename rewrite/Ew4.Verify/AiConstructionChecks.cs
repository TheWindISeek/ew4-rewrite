using System.Text.Json;
using Ew4.Core;

internal static class AiConstructionChecks
{
    public static int Run(string root)
    {
        int checks = 0;
        void Check(bool value, string label)
        {
            checks++;
            if (!value) throw new InvalidOperationException(label);
        }
        void Reject(Action action, string code)
        {
            try { action(); }
            catch (RuleViolation e) { Check(e.Code == code, code + ": " + e.Code); return; }
            throw new InvalidOperationException("Expected " + code);
        }
        var installations = LegacyAiConstruction.Installations();
        var fortresses = LegacyAiConstruction.Fortresses();
        using var native = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "reverse/reports/ai_construction_vectors.json")));
        int built = 0;
        foreach (var vector in native.RootElement.EnumerateArray())
        {
            var v = vector.GetProperty("inputs");
            bool Flag(string key) => v.GetProperty(key).GetBoolean();
            var sites = v.GetProperty("sites").EnumerateArray().ToArray();
            int Int(int i, string key) => sites[i].GetProperty(key).GetInt32();
            bool SiteFlag(int i, string key) => sites[i].GetProperty(key).GetBoolean();
            int Count(int i, int mode) => sites[i].GetProperty("counts").GetProperty(mode.ToString()).GetInt32();
            var stock = v.GetProperty("stock").EnumerateArray().Select(x => x.GetInt64()).ToArray();
            var raw = v.GetProperty("raw_random").EnumerateArray().Select(x => x.GetInt64()).ToArray();
            var bounds = new List<long>();
            int drawIndex = 0;
            long Draw(long bound) { bounds.Add(bound); return raw[drawIndex++] % bound; }
            AiConstructionChoice? choice;
            bool installation = v.GetProperty("group").GetString() == "installation";
            if (installation)
            {
                var facts = Enumerable.Range(0, sites.Length).Select(i => new AiInstallationSite(i,
                    Int(i, "building") == 255 && Int(i, "installation") == 255 && !SiteFlag(i, "sea") && SiteFlag(i, "present") && Int(i, "category") == 0
                        && Int(i, "construction") == 0 && !SiteFlag(i, "acted") && Int(i, "environment") == 0 && Count(i, 0x7da) > 0,
                    new Dictionary<string, int> { ["infantry"] = Count(i, 0x848), ["cavalry"] = Count(i, 0x852), ["artillery"] = Count(i, 0x85c), ["naval"] = Count(i, 0x870), ["fort"] = Count(i, 0x866) })).ToArray();
                choice = AiProduction.SelectInstallation(facts, new Dictionary<string, long> { ["41"] = stock[4], ["42"] = stock[6], ["43"] = stock[5] }, installations, Draw);
            }
            else
            {
                var facts = Enumerable.Range(0, sites.Length).Select(i => new AiFortressSite(i,
                    Int(i, "building") == 255 && Int(i, "installation") == 255 && !SiteFlag(i, "sea") && !SiteFlag(i, "present") && Int(i, "environment") == 0 && !SiteFlag(i, "adjacent_fort"),
                    SiteFlag(i, "coastal"), SiteFlag(i, "sea"), Count(i, 0x7f3), Count(i, 0x870), Count(i, 0x7d3), sites[i].GetProperty("neighbors").EnumerateArray()
                        .Select(n => new AiFortressNearby(!n.GetProperty("nonallied").GetBoolean(), n.GetProperty("building").GetInt32() == 255 ? null : n.GetProperty("level").GetInt32(), n.GetProperty("sea").GetBoolean())).ToArray())).ToArray();
                choice = AiProduction.SelectFortress(facts, new Dictionary<string, long> { ["37"] = stock[0], ["38"] = stock[1], ["39"] = stock[2], ["40"] = stock[3] }, !Flag("fort_disabled"), fortresses, Draw);
            }
            int[] command = [-1, -1, -1, -1, 0x10000];
            if (choice != null && Flag("card") && Flag("affordable") && Flag("permitted"))
            { command = [int.Parse(choice.RecipeId), choice.Cell, choice.Cell, installation ? choice.Cell : -1, 0x10010]; built++; }
            Check(command.SequenceEqual(vector.GetProperty("expected").EnumerateArray().Select(x => x.GetInt32())), "native " + (installation ? "installation" : "fortress") + " construction decision");
            Check(bounds.SequenceEqual(vector.GetProperty("random_bounds").EnumerateArray().Select(x => x.GetInt64())), "native construction preference/score/admission random sequence");
        }
        Check(built > 100, "native construction fixtures cover successful decisions");
        var wideRules = fortresses with { Recipes = Enumerable.Range(0, 20).Select(i => new AiFortressRecipe("fort.tier." + i, 1, false)).ToArray() };
        var wideSite = new AiFortressSite(100_000, true, false, false, 0, 0, 0, [new(true, 4, false), new(true, null, false)]);
        var wideStock = wideRules.Recipes.ToDictionary(r => r.Id, _ => 20L);
        var drawsWide = new Queue<long>([19, 59]);
        Check(AiProduction.SelectFortress([wideSite], wideStock, true, wideRules, _ => drawsWide.Dequeue())?.RecipeId == "fort.tier.19", "fortress tiers and cell IDs are data-driven without original four-tier limit");
        var recipes = Enumerable.Range(0, 32).Select(i => new AiInstallationRecipe("installation." + i, [new("new.category", 10)])).ToArray();
        var manyRules = installations with { Recipes = recipes, PreferenceOrder = recipes.Select(r => r.Id).ToArray() };
        var manyStock = recipes.ToDictionary(r => r.Id, _ => 100L);
        var manySite = new AiInstallationSite(100_001, true, new Dictionary<string, int> { ["new.category"] = 50 });
        var random = new AiRandom(1946);
        Check(AiProduction.SelectInstallation([manySite], manyStock, manyRules, random.Next) != null, "installation scoring supports new kinds and enemy categories");
        ulong before = random.State;
        Check(AiProduction.SelectFortress([wideSite], wideStock, false, wideRules, random.Next) == null && random.State == before, "disabled fortress policy consumes no random draw");
        Reject(() => AiProduction.SelectInstallation([manySite], manyStock, manyRules, _ => -1), "ai.production_random");
        Reject(() => AiProduction.SelectFortress([wideSite], wideStock, true, wideRules with { Recipes = [] }, random.Next), "ai.fortress_configuration");
        Reject(() => AiProduction.SelectFortress([wideSite], new Dictionary<string, long> { ["bad"] = -1 }, true, wideRules, random.Next), "ai.construction_stock");
        return checks;
    }
}
