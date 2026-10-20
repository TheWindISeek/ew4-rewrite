using System.Text.Json;
using Ew4.Core;
using Ew4.Persistence;

internal static class AiProductionChecks
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
        var recruitRules = new AiRecruitRules();
        var upgradeRules = new AiUpgradeRules();
        using var native = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "reverse/reports/ai_production_vectors.json")));
        foreach (var vector in native.RootElement.EnumerateArray())
        {
            var input = vector.GetProperty("inputs");
            int Int(string key) => input.GetProperty(key).GetInt32();
            bool Flag(string key) => input.GetProperty(key).GetBoolean();
            var sites = input.GetProperty("sites").EnumerateArray().ToArray();
            int SiteInt(int i, string key) => sites[i].GetProperty(key).GetInt32();
            bool SiteFlag(int i, string key) => sites[i].GetProperty(key).GetBoolean();
            var raw = input.GetProperty("raw_random").EnumerateArray().Select(x => x.GetInt64()).ToArray();
            var bounds = new List<long>();
            int draws = 0;
            long Draw(long bound) { bounds.Add(bound); return raw[draws++] % bound; }
            int[] actual = [-1, -1, -1, -1, 0x10000];
            string group = input.GetProperty("group").GetString()!;
            if (group == "recruit")
            {
                if (AiProduction.AttemptRecruit(Int("food"), Int("income"), Int("consumption"), Draw(100), recruitRules))
                {
                    var facts = Enumerable.Range(0, sites.Length).Select(i => new AiRecruitSite(i,
                        Int("request") == 33 ? SiteInt(i, "building") == 3 : SiteInt(i, "building") is 0 or 1 or 2,
                        SiteFlag(i, "present"), SiteInt(i, "movement") > 0, SiteInt(i, "morale") == -3, SiteInt(i, "behavior") == 3,
                        SiteInt(i, "relocation") == -1 ? null : SiteInt(i, "relocation"), SiteInt(i, "value"), SiteInt(i, "strength"), SiteInt(i, "threat"), SiteInt(i, "environment"))).ToArray();
                    var choice = AiProduction.SelectRecruitSite(facts, recruitRules, Draw);
                    if (choice != null && Int("offer") != 47 && Flag("card") && Flag("permitted"))
                        actual = [Int("offer"), choice.Cell, choice.Relocation ?? choice.Cell, choice.Cell, choice.Relocation.HasValue ? 0x10050 : 0x10010];
                }
            }
            else if (group == "upgrade")
            {
                var facts = Enumerable.Range(0, sites.Length).Select(i => new AiUpgradeSite(i,
                    SiteInt(i, "building") == 255 ? null : SiteInt(i, "building").ToString(), SiteInt(i, "environment"),
                    SiteInt(i, "threat"), SiteInt(i, "friendly"), SiteFlag(i, "upgrade"))).ToArray();
                int? choice = AiProduction.SelectUpgrade(facts, upgradeRules, Draw);
                if (choice.HasValue && Flag("card") && Flag("affordable") && Flag("permitted")) actual = [44, choice.Value, choice.Value, -1, 0x10010];
            }
            else
            {
                var facts = Enumerable.Range(0, sites.Length).Select(i => new AiTransportSite(i, SiteFlag(i, "present"), SiteFlag(i, "transport"),
                    SiteFlag(i, "sea"), SiteFlag(i, "coastal"), SiteInt(i, "category") != 4, SiteInt(i, "environment"), SiteInt(i, "sea_neighbors"))).ToArray();
                int? choice = AiProduction.SelectTransport(facts, Flag("auto_transport"));
                if (choice.HasValue && Flag("card")) actual = [46, choice.Value, choice.Value, choice.Value, 0x10010];
            }
            Check(actual.SequenceEqual(vector.GetProperty("expected").EnumerateArray().Select(x => x.GetInt32())), "native " + group + " production site and command");
            Check(bounds.SequenceEqual(vector.GetProperty("random_bounds").EnumerateArray().Select(x => x.GetInt64())), "native " + group + " random order and bounds");
        }
        Check(AiProduction.AttemptRecruit(long.MaxValue, long.MaxValue, 0, 99, recruitRules), "large modern food reserves do not overflow recruitment rating");
        Check(!AiProduction.AttemptRecruit(0, 0, long.MaxValue, 0, recruitRules), "large negative food balance suppresses recruitment");
        var zeroRandom = new AiRandom(0);
        var hugeUpgrade = new AiUpgradeSite(100_000, "industrial.city", 0, long.MaxValue, long.MaxValue, true);
        var customUpgrade = upgradeRules with { BuildingWeights = new Dictionary<string, long> { ["industrial.city"] = 5_000_000_000 },
            Chances = [new(0, 100)] };
        var upgradeDraws = new Queue<long>([9, 0, 0]);
        Check(AiProduction.SelectUpgrade([hugeUpgrade], customUpgrade, _ => upgradeDraws.Dequeue()) == 100_000,
            "custom building kind and 64-bit weight/threat facts have no legacy enum or score capacity");
        var many = Enumerable.Range(0, 200).Select(i => new AiUpgradeSite(i, "0", 0, 0, 1, true)).ToArray();
        var manyDraws = new Queue<long>([0, 199]);
        Check(AiProduction.SelectUpgrade(many, upgradeRules, _ => manyDraws.Dequeue()) == 199, "upgrade candidate pool exceeds native fixed-size assumptions");
        Reject(() => AiProduction.AttemptRecruit(-1, 0, 0, 0, recruitRules), "ai.recruit_economy");
        Reject(() => AiProduction.SelectUpgrade([hugeUpgrade], customUpgrade, _ => -1), "ai.production_random");
        Reject(() => AiProduction.SelectRecruitSite([], recruitRules with { Economy = [] }, zeroRandom.Next), "ai.recruit_configuration");

        var fixture = WorldChecks.Example();
        var actor = fixture.Units[1] with { Cell = 0, RemainingMovement = 6,
            Combatant = fixture.Units[1].Combatant with { Profile = fixture.Units[1].Combatant.Profile with { Grade = "0" } } };
        var enemy = fixture.Units[0] with { Cell = 4 };
        var world = fixture with { Width = 5, Height = 1, Units = [actor, enemy], Cells = Enumerable.Range(0, 5).Select(i => new CellState
            { Owner = i == 4 ? enemy.Owner : actor.Owner, BuildingKind = i == 0 ? "0" : null, BuildingLevel = i == 0 ? 1 : 0 }).ToArray(),
            Factions = fixture.Factions.Select(f => f.Id != actor.Owner ? f : f with { Resources = new(100, 100, 100),
                Recruitment = new Dictionary<string, RecruitmentGrant> { ["recruit.fixture"] = new("0") } }).ToArray() };
        var offer = new RecruitmentOffer { Id = "recruit.fixture", Template = actor.Combatant, Cost = new(30, 5, 10), BuildingKinds = ["0"], PerTurnLimit = 1 };
        byte[] before = WorldSave.Encode(world);
        var recruited = WorldActions.RelocateAndRecruit(world, world.Revision, actor.Id, 1, "new", offer);
        Check(recruited.State.Revision == world.Revision + 1 && recruited.State.Units.Single(u => u.Id == actor.Id).Cell == 1
            && recruited.State.Units.Single(u => u.Id == "new") is { Cell: 0, RemainingMovement: 0, Acted: true }, "relocate old garrison then recruit new unit as one revision");
        Check(recruited.State.Factions.Single(f => f.Id == actor.Owner).Resources == new Resources(70, 95, 90)
            && recruited.Notices.Count(n => n.Kind == "unit.recruited") == 1 && recruited.Notices.Count(n => n.Kind == "unit.moved") == 1,
            "recruit combination charges actual cost once and exposes both notices");
        var moved = WorldActions.Move(world, world.Revision, actor.Id, 1).State;
        var separate = WorldActions.Recruit(moved, moved.Revision, 0, "new", offer).State with { Revision = recruited.State.Revision };
        Check(WorldSave.Encode(separate).SequenceEqual(WorldSave.Encode(recruited.State)), "atomic relocation/recruitment equals separate legal world commands");
        Reject(() => WorldActions.RelocateAndRecruit(world, world.Revision, actor.Id, 1, "new", offer with { Cost = new(1000, 0, 0) }), "recruit.resources");
        Reject(() => WorldActions.RelocateAndRecruit(world, world.Revision, actor.Id, 1, actor.Id, offer), "recruit.identity");
        Reject(() => WorldActions.RelocateAndRecruit(world, world.Revision, actor.Id, 4, "new", offer), "move.destination");
        Check(before.SequenceEqual(WorldSave.Encode(world)), "failed relocation/recruitment rolls back movement, resources and RNGs");
        var capped = world with { Limits = new() { UnitsPerFaction = 1 } };
        Reject(() => WorldActions.RelocateAndRecruit(capped, capped.Revision, actor.Id, 1, "new", offer), "recruit.site");
        Check(capped.Units.Single(u => u.Id == actor.Id).Cell == 0, "configured faction capacity rejects combination without moving garrison");
        var stacked = world with { Limits = new() { UnitsPerCell = 2 }, Units = [actor, actor with { Id = "second" }, enemy] };
        var stackedResult = WorldActions.RelocateAndRecruit(stacked, stacked.Revision, actor.Id, 1, "new", offer).State;
        Check(stackedResult.Units.Count(u => u.Cell == 0) == 2 && stackedResult.Units.Count(u => u.Owner == actor.Owner) == 3,
            "relocation/recruitment respects configured stacking rather than forcing old one-unit slots");
        return checks;
    }
}
