using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using Ew4.Core;
using Ew4.Legacy;

internal static class LegacyStartupChecks
{
    private static LegacyBattle Example(bool[] ai, int[]? camps = null, float[][]? economy = null)
    {
        byte[] raw = new byte[72 + ai.Length * 180 + 1];
        void Int(int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(raw.AsSpan(offset), value);
        int[] header = [2, 1, 0, 0, 1, 1, ai.Length, 0, 0, 0, 0, 1, 50, 20, 0, 1, 0, 0];
        for (int i = 0; i < header.Length; i++) Int(i * 4, header[i]);
        for (int i = 0; i < ai.Length; i++)
        {
            int offset = 72 + i * 180;
            Int(offset + 12, ai[i] ? 1 : 0); Int(offset + 16, camps?[i] ?? i + 1);
            Encoding.UTF8.GetBytes("country." + i).CopyTo(raw, offset + 24);
            Encoding.UTF8.GetBytes("fra").CopyTo(raw, offset + 40);
            Int(offset + 56, BitConverter.SingleToInt32Bits(economy?[i][0] ?? 1));
            Int(offset + 60, BitConverter.SingleToInt32Bits(economy?[i][1] ?? 0));
            Int(offset + 172, 100);
        }
        raw[^1] = 255;
        return LegacyBattle.Read(raw);
    }

    public static int Run(string root)
    {
        int checks = 0;
        void Check(bool value, string label) { checks++; if (!value) throw new InvalidOperationException("Legacy startup: " + label); }
        void Reject(Action action, string code)
        {
            try { action(); } catch (RuleViolation error) { Check(error.Code == code, code + " actual=" + error.Code); return; }
            throw new InvalidOperationException("Expected startup rejection: " + code);
        }
        string assets = Path.Combine(root, "apk_work/original/assets");
        var technology = LegacyWarzoneTechnology.Read(File.ReadAllText(Path.Combine(assets, "def_warzonetech.xml")));
        using var vectors = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "reverse/reports/startup_vectors.json")));
        foreach (var vector in vectors.RootElement.GetProperty("ordering").EnumerateArray())
        {
            var ai = vector.GetProperty("ai").EnumerateArray().Select(v => v.GetBoolean()).ToArray();
            var source = Example(ai);
            if (ai.All(v => v)) { Reject(() => LegacyScenarioInitialization.Prepare(source, 1, null, technology), "legacy.start_player"); continue; }
            var result = LegacyScenarioInitialization.Prepare(source, 1, null, technology);
            Check(result.TurnOrder.SequenceEqual(vector.GetProperty("expected").EnumerateArray().Select(v => v.GetInt32())), "native first-human promotion preserves other faction order");
            Check(result.PlayerIndex == Array.IndexOf(ai, false) && !result.Records[result.PlayerIndex].Ai
                && result.Victory.RoundLimit == 50 && !result.Victory.ObjectivesEnabled && result.HighestRatingRound == 20, "startup player and BTL victory/rating thresholds");
            Check(source.Factions.Select(f => f.Ai).SequenceEqual(ai) && source.Factions.All(f => f.Values68.All(v => v == 0)), "startup leaves original records unchanged");
        }
        foreach (var vector in vectors.RootElement.GetProperty("dispatch").EnumerateArray())
        {
            int mode = vector.GetProperty("mode").GetInt32(), chosen = vector.GetProperty("selected").GetInt32();
            if (mode != 2) continue;
            var source = Example([true, true, true]);
            var result = LegacyScenarioInitialization.Prepare(source, mode, "country." + chosen, technology);
            Check(result.Records.Select(f => f.Ai).SequenceEqual(vector.GetProperty("expected_ai").EnumerateArray().Select(v => v.GetInt32() != 0)), "native selected conquest faction becomes human");
            Check(result.TurnOrder[0] == chosen && source.Factions.All(f => f.Ai), "selection uses stable source identity without mutating BTL");
        }
        int normalization = 0;
        foreach (var vector in vectors.RootElement.GetProperty("initialization").EnumerateArray())
        {
            int mode = vector.GetProperty("mode").GetInt32(), player = vector.GetProperty("player").GetInt32();
            var camps = vector.GetProperty("camps").EnumerateArray().Select(v => v.GetInt32()).ToArray();
            var economy = vector.GetProperty("initial").EnumerateArray().Select(v => v.EnumerateArray().Select(x => x.GetSingle()).ToArray()).ToArray();
            var snapshot = vector.GetProperty("technology").EnumerateArray().Select(v => v.GetInt32()).ToArray();
            var source = Example(Enumerable.Range(0, camps.Length).Select(i => i != player).ToArray(), camps, economy);
            var result = LegacyScenarioInitialization.Prepare(source, mode, mode == 2 ? "country." + player : null, technology, technologySnapshot: snapshot);
            Check(result.TurnOrder.SequenceEqual(vector.GetProperty("order").EnumerateArray().Select(v => v.GetInt32())), "native new-game dispatcher order");
            for (int i = 0; i < camps.Length; i++)
            {
                var expected = vector.GetProperty("expected")[i];
                Check(result.Records[i].IncomeMultiplier == expected[0].GetSingle() && result.Records[i].AiEvasionBonus == expected[1].GetSingle(), "native same-camp income and evasion normalization");
                Check(result.Records[i].Values68.SequenceEqual(mode == 1 && i == player ? snapshot : source.Factions[i].Values68), "native new-game technology affects selected player in mode 1 only");
                Check(result.Records[i].Resources == source.Factions[i].Resources && result.Records[i].Count172 == 100, "initialization preserves resources and configurable deployment quota");
                normalization++;
            }
            Check(result.SourceRevision == LegacyScenarioInitialization.Prepare(source, mode, mode == 2 ? "country." + player : null, technology, technologySnapshot: snapshot).SourceRevision,
                "initialization fingerprint is deterministic");
        }
        int scenarios = 0, conquestChoices = 0;
        foreach (string path in Directory.GetFiles(assets, "*.btl").Order(StringComparer.Ordinal))
        {
            string name = Path.GetFileNameWithoutExtension(path);
            var battle = LegacyBattle.Read(File.ReadAllBytes(path));
            int mode = name.StartsWith("conquest", StringComparison.Ordinal) ? 2 : name.StartsWith("tutorial", StringComparison.Ordinal) ? 4 : 1;
            var choices = mode == 2 ? battle.Factions.Select(f => f.CountryId).ToArray() : new string?[] { null };
            foreach (string? choice in choices)
            {
                var initialized = LegacyScenarioInitialization.Prepare(battle, mode, choice, technology);
                Check(initialized.TurnOrder.Count == battle.Factions.Count && initialized.TurnOrder.Distinct().Count() == battle.Factions.Count
                    && initialized.TurnOrder[0] == initialized.PlayerIndex, "real scenario has a complete stable turn order");
                Check(initialized.Victory.RoundLimit == battle.Header[12] && initialized.Victory.ObjectivesEnabled == (battle.Header[11] == 0), "real scenario victory policy from header");
                if (mode == 2) { Check(initialized.Records[initialized.PlayerIndex].CountryId == choice, "every original conquest country can be selected explicitly"); conquestChoices++; }
            }
            scenarios++;
        }
        var example = Example([true, false, true]);
        var repeatedBytes = Example([false, true]).ToBytes();
        Array.Copy(repeatedBytes, 72 + 24, repeatedBytes, 72 + 180 + 24, 16);
        var repeated = LegacyBattle.Read(repeatedBytes);
        var repeatedStart = LegacyScenarioInitialization.Prepare(repeated, 1, null, technology);
        Check(repeatedStart.Records.Count == 2 && repeatedStart.TurnOrder.SequenceEqual(new[] { 0, 1 })
            && LegacyScenarioInitialization.FactionId("duplicate", 0, repeated.Factions[0]) != LegacyScenarioInitialization.FactionId("duplicate", 1, repeated.Factions[1]),
            "same named country keeps two independent indexed factions");
        Reject(() => LegacyScenarioInitialization.Prepare(example, 3, null, technology), "legacy.start_mode");
        Reject(() => LegacyScenarioInitialization.Prepare(example, 2, "missing", technology), "legacy.start_player");
        Reject(() => LegacyScenarioInitialization.Prepare(example, 1, "country.1", technology), "legacy.start_player");
        Reject(() => LegacyScenarioInitialization.Prepare(example, 2, "country.0", technology), "legacy.start_player");
        var fingerprint = LegacyScenarioInitialization.Prepare(example, 1, null, technology);
        Check(fingerprint.SourceRevision != LegacyScenarioInitialization.Prepare(example, 4, null, technology).SourceRevision, "startup mode participates in rule binding");
        Check(fingerprint.SourceRevision != LegacyScenarioInitialization.Prepare(example, 1, null, technology, technologySnapshot: Enumerable.Repeat(5, 26).ToArray()).SourceRevision, "actual technology participates in rule binding");
        File.WriteAllText(Path.Combine(root, "rewrite/reports/legacy_startup.json"), JsonSerializer.Serialize(new { checks, errors = 0, scenarios, conquestChoices,
            normalizedFactions = normalization, limitation = "Startup faction selection/order/normalization and victory metadata only; full scene/world initialization, profile migration, sessions and client integration remain pending." }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
        return checks;
    }
}
