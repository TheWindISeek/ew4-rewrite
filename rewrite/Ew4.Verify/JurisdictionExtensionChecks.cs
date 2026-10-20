using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Ew4.Client;
using Ew4.Core;
using Ew4.Legacy;
using Ew4.Persistence;

internal static class JurisdictionExtensionChecks
{
    public static int Run(string root)
    {
        int checks = 0;
        void Check(bool value, string label) { if (!value) throw new Exception("JURI: " + label); checks++; }
        void Reject(Action action, string label)
        {
            try { action(); } catch (RuleViolation) { checks++;return; }
            throw new Exception("JURI accepted invalid data: " + label);
        }
        string assets = Path.Combine(root, "rewrite/data/original");
        byte[] Read(string p) => File.ReadAllBytes(p.StartsWith("original/", StringComparison.Ordinal)
            ? Path.Combine(assets, p[9..]) : Path.Combine(root, "rewrite/data/legacy", p[5..]));
        var content = new ClientContent(Read, Directory.EnumerateFiles(assets, "*.btl"));
        foreach (string file in Directory.EnumerateFiles(assets, "*.btl"))
        {
            byte[] raw = File.ReadAllBytes(file);var old = LegacyBattle.Read(raw);
            Check(old.Jurisdictions == null && old.ToBytes().SequenceEqual(raw) && old.ToLegacyBytes().SequenceEqual(raw), "unchanged legacy bytes " + Path.GetFileName(file));
        }
        const string id = "conquest1";
        var battle = content.Battle(id);var session = content.Start(id, "fra").Session;var world = session.World;
        LegacyJurisdictionData Document(WorldState w, LegacyBattle b)
        {
            var cells = new List<int>();
            for (int y = 0; y < b.Height; y++) for (int x = 0; x < b.Width; x++) cells.Add(w.Jurisdictions!.CellCenters[(y + b.Top) * w.Width + x + b.Left]);
            return new(1, b.MapId, w.Width, w.Height, b.Left, b.Top, b.Width, b.Height, Convert.ToHexStringLower(SHA256.HashData(b.ToLegacyBytes())), cells);
        }
        var document = Document(world, battle);
        var auto = TerritorialJurisdictions.Create(world);
        Check(auto.CellCenters.SequenceEqual(world.Jurisdictions!.CellCenters), "old files retain automatic generation");
        int cell = -1, other = -1;
        var map = new HexMap(world.Width, world.Height);var visited = new HashSet<int>();
        for (int seed = 0; seed < world.Cells.Count && cell < 0; seed++)
        {
            if (visited.Contains(seed) || !world.Cells[seed].Terrain.Active || world.Cells[seed].Terrain.Sea || world.Cells[seed].BuildingKind == "port") continue;
            var connected = new HashSet<int> { seed };var queue = new Queue<int>();queue.Enqueue(seed);
            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                for (int d = 0; d < 6; d++)
                {
                    int next = map.Neighbor(current, d);
                    if (next < 0 || !world.Cells[next].Terrain.Active || world.Cells[next].Terrain.Sea || world.Cells[next].BuildingKind == "port" || !connected.Add(next)) continue;
                    queue.Enqueue(next);
                }
            }
            visited.UnionWith(connected);
            var roots = connected.Where(c => auto.CellCenters[c] == c).ToArray();
            if (roots.Length < 2) continue;
            cell = connected.First(i => world.Cells[i].BuildingKind == null && auto.CellCenters[i] >= 0);
            other = roots.First(c => c != auto.CellCenters[cell]);
        }
        Check(cell >= 0 && other >= 0, "fixture has a land component with multiple district centers");
        int local = (cell / world.Width - battle.Top) * battle.Width + cell % world.Width - battle.Left;
        var manual = document.CellCenters.ToArray();manual[local] = other;
        document = document with { CellCenters = manual };
        var extended = battle.WithJurisdictions(document);var parsed = LegacyBattle.Read(extended);
        Check(extended.Length == battle.LegacyLength + 96 + 4 * battle.Width * battle.Height, "specified binary size");
        Check(parsed.Jurisdictions!.CellCenters.SequenceEqual(manual), "binary map roundtrip");
        Check(parsed.ToLegacyBytes().SequenceEqual(battle.ToBytes()), "original BTL body retained exactly");
        Check(parsed.WithJurisdictions(null).SequenceEqual(battle.ToBytes()), "strip restores original file");
        Check(LegacyBattle.Read(parsed.WithJurisdictions(document)).Jurisdictions!.CellCenters.SequenceEqual(manual), "replace existing block without duplication");
        var extendedContent = new ClientContent(p => p == "original/" + id + ".btl" ? extended : Read(p), [id + ".btl"]);
        var started = extendedContent.Start(id, "fra").Session;
        Check(started.World.Jurisdictions!.CellCenters[cell] == other && auto.CellCenters[cell] != other, "explicit BTL overrides automatic partition at actual startup");
        var saved = SessionSave.Decode(SessionSave.Encode(started), new(started.ContentRevision, started.World.ContentRevision, started.Account.ContentRevision));
        Check(saved.World.Jurisdictions!.CellCenters.SequenceEqual(started.World.Jurisdictions.CellCenters), "save retains explicit partitions");
        Check(started.ContentRevision != session.ContentRevision && started.World.ContentRevision != session.World.ContentRevision, "BTL extension changes save bindings");
        Reject(() => SessionSave.Decode(SessionSave.Encode(session), new(started.ContentRevision, started.World.ContentRevision, started.Account.ContentRevision)), "old automatic save cannot be silently rebound");
        var changedCells = started.World.Cells.ToArray();changedCells[other] = changedCells[other] with { Owner = world.ActiveFaction };
        var changedWorld = started.World with { Cells = changedCells };
        changedWorld.Validate();Check(changedWorld.Jurisdictions.CellCenters[cell] == other, "center ownership changes never repartition");
        string winner = world.ActiveFaction, previous = world.Factions.First(f => f.Id != winner).Id;
        var captureCells = started.World.Cells.ToArray();
        captureCells[other] = captureCells[other] with { Owner = previous };captureCells[cell] = captureCells[cell] with { Owner = winner };
        var captureBefore = started.World with { Cells = captureCells, Units = [], Factions = world.Factions.Select(f => f with { Camp = "allied" }).ToArray() };
        Check(!JurisdictionClaims.CanBuildFort(captureBefore, cell, winner), "manual center ownership gates construction even on owned tile");
        captureBefore = captureBefore with { Cells = captureBefore.Cells.Select((c, i) => i == cell ? c with { Owner = previous } : c).ToArray() };
        var proposed = captureBefore.Cells.ToArray();proposed[other] = proposed[other] with { Owner = winner };
        int neighbor = new HexMap(world.Width, world.Height).Neighbor(other, 0);
        var claimed = JurisdictionClaims.Move(captureBefore, proposed, [neighbor, other], winner);
        Check(claimed[cell].Owner == winner && claimed[other].Owner == winner, "capture follows authored jurisdiction");
        Check(JurisdictionClaims.CanBuildFort(captureBefore with { Cells = claimed }, cell, winner), "captured authored center enables construction");
        var noDistrict = manual.ToArray();noDistrict[local] = -1;
        Check(LegacyBattle.Read(battle.WithJurisdictions(document with { CellCenters = noDistrict })).Jurisdictions!.Apply(world).CellCenters[cell] == -1, "explicit unassigned land remains unassigned");
        Reject(() => battle.WithJurisdictions(document with { Schema = 2 }), "JSON schema");
        Reject(() => battle.WithJurisdictions(document with { MapId = document.MapId + 1 }), "map identity");
        Reject(() => battle.WithJurisdictions(document with { BaseSha256 = new string('0', 64) }), "source fingerprint");
        Reject(() => battle.WithJurisdictions(document with { CellCenters = manual[..^1] }), "incomplete map");
        var invalid = manual.ToArray();invalid[local] = world.Cells.Count;
        Reject(() => battle.WithJurisdictions(document with { CellCenters = invalid }), "out of range center");
        invalid[local] = -2;Reject(() => battle.WithJurisdictions(document with { CellCenters = invalid }), "invalid sentinel");
        invalid[local] = cell;
        Reject(() => LegacyBattle.Read(battle.WithJurisdictions(document with { CellCenters = invalid })).Jurisdictions!.Apply(world), "ordinary tile is not a center");
        int center = auto.CellCenters.First(c => c >= 0), centerLocal = (center / world.Width - battle.Top) * battle.Width + center % world.Width - battle.Left;
        invalid = manual.ToArray();invalid[centerLocal] = -1;
        Reject(() => LegacyBattle.Read(battle.WithJurisdictions(document with { CellCenters = invalid })).Jurisdictions!.Apply(world), "center must point to itself");
        var global = started.World.Jurisdictions.CellCenters.ToArray();
        int sea = Enumerable.Range(0, global.Length).First(i => world.Cells[i].Terrain.Active && world.Cells[i].Terrain.Sea);
        global[sea] = center;Reject(() => TerritorialJurisdictions.FromAssignments(world, global), "water cannot be assigned");
        int port = Enumerable.Range(0, global.Length).First(i => world.Cells[i].Terrain.Active && world.Cells[i].BuildingKind == "port");
        global = started.World.Jurisdictions.CellCenters.ToArray();global[port] = center;
        Reject(() => TerritorialJurisdictions.FromAssignments(world, global), "ports remain independent");
        var islandCells = world.Cells.Select(c => c with { Terrain = c.Terrain with { Sea = true }, BuildingKind = null }).ToArray();
        islandCells[center] = world.Cells[center];islandCells[cell] = world.Cells[cell];
        global = Enumerable.Repeat(-1, global.Length).ToArray();global[center] = center;global[cell] = center;
        var islands = TerritorialJurisdictions.FromAssignments(world with { Cells = islandCells }, global);
        Check(islands.DetachedCells.Contains(cell) && islands.Borders.Count == 0, "overseas labels never produce land frontiers");
        byte[] Mutate(int offset, int value) { var copy = extended.ToArray();BinaryPrimitives.WriteInt32LittleEndian(copy.AsSpan(battle.LegacyLength + offset), value);return copy; }
        Reject(() => LegacyBattle.Read(extended[..^1]), "truncated payload");
        Reject(() => LegacyBattle.Read(Mutate(8, 2)), "unknown container version");
        Reject(() => LegacyBattle.Read(Mutate(12, 65)), "block count budget");
        Reject(() => LegacyBattle.Read(Mutate(20, 2)), "unknown JURI version");
        Reject(() => LegacyBattle.Read(Mutate(24, 0)), "JURI cannot be optional");
        Reject(() => LegacyBattle.Read(Mutate(28, int.MaxValue)), "bad block size");
        Reject(() => LegacyBattle.Read(Mutate(64, 0)), "bound body hash mismatch");
        Reject(() => LegacyBattle.Read(extended.Concat(new byte[] { 0 }).ToArray()), "unrecognized tail");
        byte[] Blocks(params (string Tag, uint Version, uint Flags, byte[] Payload)[] blocks)
        {
            using var stream = new MemoryStream();stream.Write(battle.ToBytes());using var writer = new BinaryWriter(stream);
            writer.Write("EW4XBTL\0"u8);writer.Write(1u);writer.Write((uint)blocks.Length);
            foreach (var b in blocks) { writer.Write(System.Text.Encoding.ASCII.GetBytes(b.Tag));writer.Write(b.Version);writer.Write(b.Flags);writer.Write((uint)b.Payload.Length);writer.Write(b.Payload); }
            return stream.ToArray();
        }
        byte[] payload = extended[(battle.LegacyLength + 32)..];
        Reject(() => LegacyBattle.Read(Blocks(("JURI", 1, 1, payload), ("JURI", 1, 1, payload))), "duplicate JURI");
        Reject(() => LegacyBattle.Read(Blocks(("TEST", 1, 1, [1]))), "unknown required block");
        var optional = LegacyBattle.Read(Blocks(("TEST", 7, 0, [1, 2]), ("JURI", 1, 1, payload)));
        Check(optional.Jurisdictions != null && LegacyBattle.Read(optional.WithJurisdictions(document)).ToBytes().SequenceEqual(optional.ToBytes()), "unknown optional block preserved during replacement");
        Check(LegacyBattle.Read(optional.WithJurisdictions(null)).Jurisdictions == null, "strip only removes JURI");
        var tutorial = content.Battle("tutorials1");var tutorialWorld = content.Start("tutorials1", null).Session.World;
        var tutorialData = new LegacyJurisdictionData(1, tutorial.MapId, tutorialWorld.Width, tutorialWorld.Height, tutorial.Left, tutorial.Top, tutorial.Width, tutorial.Height,
            Convert.ToHexStringLower(SHA256.HashData(tutorial.ToLegacyBytes())), Enumerable.Repeat(-1, tutorial.Width * tutorial.Height).ToArray());
        var tutorialBytes = tutorial.WithJurisdictions(tutorialData);
        var tutorialContent = new ClientContent(p => p == "original/tutorials1.btl" ? tutorialBytes : Read(p), ["tutorials1.btl"]);
        Reject(() => tutorialContent.Start("tutorials1", null), "tutorial cannot silently ignore explicit jurisdiction data");
        foreach (var choice in content.Scenarios.Where(s => s.Mode is 1 or 2))
        {
            var b = content.Battle(choice.Id);var w = content.Start(choice.Id, choice.Mode == 2 ? b.Factions.First(f => f.Camp != 4).CountryId : null).Session.World;
            var doc = Document(w, b);var reload = LegacyBattle.Read(b.WithJurisdictions(doc)).Jurisdictions!.Apply(w);
            Check(reload.CellCenters.SequenceEqual(w.Jurisdictions!.CellCenters), "automatic export can be written back: " + choice.Id);
        }
        string folder = Path.Combine(root, "rewrite/reports/jurisdiction-extension-20261020");Directory.CreateDirectory(folder);
        File.WriteAllBytes(Path.Combine(folder, id + ".btl"), extended);
        File.WriteAllText(Path.Combine(folder, "jurisdiction.json"), JsonSerializer.Serialize(document, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }));
        File.WriteAllText(Path.Combine(folder, "core.json"), JsonSerializer.Serialize(new { checks, errors = 0, changedCell = cell, automaticCenter = auto.CellCenters[cell], manualCenter = other, bytes = extended.Length - battle.LegacyLength }));
        return checks;
    }
}
