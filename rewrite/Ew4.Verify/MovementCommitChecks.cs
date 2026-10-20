using System.Text.Json;
using Ew4.Core;
using Ew4.Persistence;

internal static class MovementCommitChecks
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
            try { action(); } catch (RuleViolation error) { Check(error.Code == code, code + " != " + error.Code); return; }
            throw new InvalidOperationException("Expected " + code);
        }
        using var data = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "reverse/reports/movement_commit_vectors.json")));
        string?[] owners = [null, "winner", "loser"];
        foreach (var vector in data.RootElement.EnumerateArray())
        {
            var facts = vector.GetProperty("tiles").EnumerateArray().ToArray();
            var cells = facts.Select(f => new CellState
            {
                Owner = owners[f.GetProperty("owner").GetInt32()], Objective = f.GetProperty("objective").GetBoolean(),
                Terrain = new() { Sea = f.GetProperty("sea").GetBoolean(), Active = f.GetProperty("active").GetBoolean() },
                BuildingKind = f.GetProperty("building").ValueKind == JsonValueKind.Null ? null : f.GetProperty("building").GetInt32() == 4 ? "farmland" : "city"
            }).ToArray();
            var route = vector.GetProperty("route").EnumerateArray().Select(v => v.GetInt32()).ToArray();
            var occupied = facts.Select((f, i) => (f, i)).Where(p => p.f.GetProperty("occupied").GetBoolean() && p.i != route[0]).Select(p => p.i).ToArray();
            bool moved = route.Length > 1 && vector.GetProperty("source_count").GetInt32() > 0 && vector.GetProperty("movement").GetInt32() > 0
                && !vector.GetProperty("stunned").GetBoolean() && !facts[route[^1]].GetProperty("occupied").GetBoolean();
            var actual = moved ? TerritoryClaims.Move(new(5, 3), cells, occupied, route, "winner") : cells;
            var expected = vector.GetProperty("expected");
            Check(actual.Select(c => c.Owner).SequenceEqual(expected.GetProperty("owners").EnumerateArray().Select(v => owners[v.GetInt32()])), "native route territory claims");
        }
        var fixture = WorldChecks.Example();
        var own = fixture.Units[1] with { Cell = 0, RemainingMovement = 6 };
        var enemy = fixture.Units[0] with { Cell = 14 };
        var state = fixture with
        {
            Width = 5, Height = 3, Units = [own, enemy],
            Cells = Enumerable.Range(0, 15).Select(i => new CellState { Owner = i == 0 ? "winner" : "loser" }).ToArray()
        };
        byte[] before = WorldSave.Encode(state);
        var movedState = WorldActions.Move(state, 0, own.Id, 2).State;
        var movedUnit = movedState.Units.Single(u => u.Id == own.Id);
        Check(movedUnit.Cell == 2 && movedUnit.RemainingMovement == 0 && !movedUnit.Acted, "move spends movement but keeps attack available");
        Check(movedState.Cells[1].Owner == "winner" && movedState.Cells[2].Owner == "winner", "path and endpoint captured");
        Check(before.SequenceEqual(WorldSave.Encode(state)), "movement detaches previous state");
        Check(WorldSave.Decode(WorldSave.Encode(movedState), "fixture.1").Units.Single(u => u.Id == own.Id).Cell == 2, "movement save continuation");
        Reject(() => WorldActions.Move(state, -1, own.Id, 2), "action.stale");
        Reject(() => WorldActions.Move(state, 0, own.Id, own.Cell), "move.destination");
        Reject(() => WorldActions.Move(state, 0, own.Id, enemy.Cell), "move.destination");
        Reject(() => WorldActions.Move(state, 0, enemy.Id, 2), "move.unit");
        Reject(() => WorldActions.Move(movedState, movedState.Revision, own.Id, 3), "move.unit");
        var shortBudget = state with { Units = [own with { RemainingMovement = 3 }, enemy] };
        Reject(() => WorldActions.Move(shortBudget, 0, own.Id, 2), "move.unreachable");
        var sea = state with { Cells = state.Cells.Select((c, i) => i == 1 ? c with { Terrain = c.Terrain with { Sea = true } } : c).ToArray() };
        Reject(() => WorldActions.Move(sea, 0, own.Id, 1), "move.unreachable");
        var transported = sea with { Units = [own with { Transport = true }, enemy] };
        var embarked = WorldActions.Move(transported, 0, own.Id, 1).State;
        Check(embarked.Units.Single(u => u.Id == own.Id).Combatant.AtSea, "transport entry updates combat context");
        var fromSea = state with { Cells = state.Cells.Select((c, i) => i == 0 ? c with { Terrain = c.Terrain with { Sea = true } } : c).ToArray(),
            Units = [own with { Transport = true, Combatant = own.Combatant with { AtSea = true } }, enemy] };
        var landed = WorldActions.Move(fromSea, 0, own.Id, 1).State;
        Check(landed.Cells[0].Owner == null && !landed.Units.Single(u => u.Id == own.Id).Combatant.AtSea, "empty sea relinquished on departure");
        var withStack = fromSea with { Limits = new() { UnitsPerCell = 2 }, Units = [fromSea.Units[0], own with { Id = "other", Transport = true }, enemy] };
        Check(WorldActions.Move(withStack, 0, own.Id, 1).State.Cells[0].Owner == "winner", "stacked garrison retains sea ownership");
        var alliedTarget = state with { Limits = new() { UnitsPerCell = 2 }, Units = [own, own with { Id = "ally", Cell = 1 }, enemy] };
        Check(WorldActions.Move(alliedTarget, 0, own.Id, 1).State.Units.Count(u => u.Cell == 1) == 2, "move honors configured stacking capacity");
        var lastCity = state with { Units = [own], Cells = state.Cells.Select((c, i) => c with { Owner = i == 2 ? "loser" : "winner", BuildingKind = i == 2 ? "city" : null }).ToArray() };
        var conquered = WorldActions.Move(lastCity, 0, own.Id, 2).State;
        Check(conquered.Factions.Single(f => f.Id == "loser").Defeated && conquered.Revision == 1, "last asset capture and surrender one command revision");
        Check(Victory.Evaluate("b", 1, conquered.Factions.Select(f => new FactionFacts(f.Id, f.Camp, f.Human, f.Defeated, 0)).ToArray(), new())
            == BattleOutcome.Victory, "capture integrates with victory rules");
        var keepRemaining = WorldActions.Move(state, 0, own.Id, 1, new() { SpendAllMovement = false }).State;
        Check(keepRemaining.Units.Single(u => u.Id == own.Id).RemainingMovement == 3, "movement consumption configurable");
        var blocked = state with { Cells = state.Cells.Select((c, i) => i == 1 ? c with { Terrain = c.Terrain with { Active = false } } : c).ToArray() };
        Reject(() => WorldActions.Move(blocked, 0, own.Id, 1), "move.unreachable");
        return checks;
    }
}
