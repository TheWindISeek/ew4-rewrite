using System.Text.Json;
using Ew4.Client;
using Ew4.Core;
using Ew4.Persistence;

internal static class FortConstructionChecks
{
    public static int Run(ClientContent content, string root)
    {
        int checks = 0;
        void Check(bool pass, string label) { checks++; if (!pass) throw new InvalidOperationException("Fort construction: " + label); }
        string folder = Path.Combine(root, "rewrite/reports/fort-construction-20261021");
        Directory.CreateDirectory(folder);
        var start = content.Start("conquest1", "fra");
        var basis = start.Session.World;
        string player = start.Session.PlayerFaction;
        var offers = start.Rules.Recruitment[player].Values.Where(o => o.Template?.Profile.Category == "fort").ToArray();
        var source = basis with { Factions = basis.Factions.Select(f => f.Id == player ? f with {
            Resources = new(100000, 100000, 100000),
            Recruitment = f.Recruitment.Concat(offers.Where(o => !f.Recruitment.ContainsKey(o.Id))
                .Select(o => new KeyValuePair<string, RecruitmentGrant>(o.Id, new()))).ToDictionary(p => p.Key, p => p.Value)
        } : f).ToArray() };
        WorldState NextOwnTurn(WorldState world)
        {
            for (int n = 0; n <= world.Factions.Count; n++)
            {
                world = WorldTurns.EndTurn(world, world.Revision, start.Rules.Turns).State;
                while (world.PendingDialogue != null || world.Phase != TurnPhase.Active)
                {
                    Check(world.Phase != TurnPhase.Finished, "fixture remains active");
                    world = WorldTurns.Resume(world, world.Revision, start.Rules.Turns, world.PendingDialogue).State;
                }
                if (world.ActiveFaction == player) return world;
            }
            throw new InvalidOperationException("Own turn not reached");
        }
        var stages = new List<object>();
        foreach (var offer in offers)
        {
            int cell = Enumerable.Range(0, source.Cells.Count).First(c => WorldActions.CanRecruit(source, c, offer));
            string id = "construction.fort";
            var world = WorldActions.Recruit(source, source.Revision, cell, id, offer).State;
            for (int remaining = offer.ConstructionTurns; remaining >= 0; remaining--)
            {
                var fort = world.Units.Single(u => u.Id == id);
                Check(fort.Combatant.ConstructionTurns == remaining, "one construction step per own turn");
                var block = AttackLegality.Check(true, true, remaining, fort.Acted, 0, fort.Combatant.Profile.MinimumRange,
                    fort.Combatant.Profile.MinimumRange, fort.Combatant.Profile.MaximumRange);
                Check(remaining > 0 ? block == AttackBlock.UnderConstruction : block == AttackBlock.None, "attack resumes only after completion");
                if (remaining > 0)
                    Check(!Ew4Combat.Calculate(basis.Units.First(u => u.Combatant.Profile.Category == "infantry").Combatant,
                        fort.Combatant, fort.Combatant.Profile.MinimumRange, new Ew4Random(42)).Counterattack, "construction cannot retaliate");
                string name = offer.Template!.Profile.Id + "-" + remaining;
                var slot = new ClientSlot("conquest1", "fra", SessionSave.Encode(start.Session with { World = world }));
                byte[] data = JsonSerializer.SerializeToUtf8Bytes(slot, ClientGame.Json);
                File.WriteAllBytes(Path.Combine(folder, name + ".json"), data);
                string profile = Path.Combine(folder, "clients", Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(profile);
                File.WriteAllBytes(Path.Combine(profile, "session.json"), data);
                var host = new ClientGame(content, profile);
                using var response = JsonDocument.Parse(host.Dispatch("/api/load", "{}"));
                Check(response.RootElement.GetProperty("ok").GetBoolean(), "fixture loads");
                var projected = response.RootElement.GetProperty("data").GetProperty("units").EnumerateArray()
                    .Single(u => u.GetProperty("id").GetString() == id);
                Check(projected.GetProperty("constructionTurns").GetInt32() == remaining, "saved construction progress reaches renderer");
                if (remaining > 0)
                    Check(!projected.GetProperty("canMove").GetBoolean() && !projected.GetProperty("canAttack").GetBoolean(), "construction has no available action badge");
                stages.Add(new { name, id, cell, profile = fort.Combatant.Profile.Id, remaining });
                if (remaining > 0) world = NextOwnTurn(world);
            }
        }
        File.WriteAllText(Path.Combine(folder, "backend.json"), JsonSerializer.Serialize(new { checks, errors = 0, stages }, ClientGame.Json));
        return checks;
    }
}
