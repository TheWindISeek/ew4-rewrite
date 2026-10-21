using System.Text.Json;
using Ew4.Client;

internal static class ClientWorkflowChecks
{
    public static int Run(ClientContent content, string folder)
    {
        int checks = 0;
        void Check(bool value, string reason) { checks++; if (!value) throw new InvalidOperationException("Client workflow: " + reason); }
        string saveRoot = Path.Combine(folder, "workflows-" + Guid.NewGuid().ToString("N"));
        var host = new ClientGame(content, saveRoot);
        JsonElement Call(string route, object? command = null, string? error = null)
        {
            using var doc = JsonDocument.Parse(host.Dispatch("/api/" + route, JsonSerializer.Serialize(command ?? new { }, ClientGame.Json)));
            var response = doc.RootElement;
            Check(response.GetProperty("ok").GetBoolean() == (error == null), route + ": " + response.GetRawText()[..Math.Min(160, response.GetRawText().Length)]);
            if (error != null) Check(response.GetProperty("code").GetString() == error, "expected " + error);
            return response.GetProperty(error == null ? "data" : "code").Clone();
        }
        long Revision(JsonElement data) => data.GetProperty("revision").GetInt64();
        var hq = Call("headquarters");
        foreach (var track in new[] { "rank", "title" })
        {
            var costs = hq.GetProperty("progression").GetProperty(track);
            Check(costs.GetArrayLength() == (track == "rank" ? content.Catalogs.Combat.RankProgression : content.Catalogs.Combat.TitleProgression).Levels.Count
                && costs[costs.GetArrayLength() - 1].ValueKind == JsonValueKind.Null
                && costs.EnumerateArray().SkipLast(1).All(c => c.GetInt64() > 0), "configured " + track + " experience thresholds and final stage exposed");
        }
        Check(hq.GetProperty("princesses").GetArrayLength() == 8 && hq.GetProperty("princesses").EnumerateArray().All(p => p.GetProperty("unlocked").GetBoolean()), "all eight old-friends rewards exposed");
        string reward = hq.GetProperty("princesses")[0].GetProperty("id").GetString()!;
        hq = Call("commander", new { op = "claim", revision = Revision(hq), offer = reward });
        string receiver = hq.GetProperty("roster")[0].GetProperty("id").GetString()!;
        Call("commander", new { op = "claim", revision = Revision(hq), offer = reward }, "account.reward_claimed");
        string donorReward = hq.GetProperty("princesses")[1].GetProperty("id").GetString()!;
        hq = Call("commander", new { op = "claim", revision = Revision(hq), offer = donorReward });
        string donor = hq.GetProperty("roster")[1].GetProperty("id").GetString()!;
        var shop = Call("commerce", new { cell = -1 });
        string stock = shop.GetProperty("offers").GetRawText();
        Check(Call("commerce", new { cell = -1 }).GetProperty("offers").GetRawText() == stock, "reopening shop preserves stock");
        var equipmentOffers = shop.GetProperty("offers").EnumerateArray().Where(o => o.GetProperty("available").GetBoolean()
            && !shop.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetString() == o.GetProperty("itemId").GetString()).GetProperty("consumable").GetBoolean()).ToArray();
        bool RequiresFlag(JsonElement o) => content.Catalogs.Items.Items.Values.Single(i => i.Equipment.Id == o.GetProperty("itemId").GetString()).Equipment.RequiresFlagSkill;
        var offer = equipmentOffers.OrderBy(RequiresFlag).First();
        string item = offer.GetProperty("itemId").GetString()!;
        shop = Call("commerce", new { cell = -1, op = "buy", revision = Revision(shop), offer = offer.GetProperty("id").GetString() });
        Check(shop.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetString() == item).GetProperty("quantity").GetInt64() == 1, "shop grants one item");
        hq = Call("headquarters");
        if (RequiresFlag(offer))
            Call("commander", new { op = "equip", revision = Revision(hq), commander = receiver, item }, "equipment.unavailable");
        else
        {
            hq = Call("commander", new { op = "equip", revision = Revision(hq), commander = receiver, item });
            Check(hq.GetProperty("roster")[0].GetProperty("items").EnumerateArray().Any(i => i.GetString() == item), "equipment assigned");
            shop = Call("commerce", new { cell = -1 });
            Call("commerce", new { cell = -1, op = "sell", revision = Revision(shop), item }, "trading.unavailable");
            hq = Call("commander", new { op = "unequip", revision = Revision(hq), commander = receiver, item });
        }
        shop = Call("commerce", new { cell = -1 });
        shop = Call("commerce", new { cell = -1, op = "sell", revision = Revision(shop), item });
        Check(shop.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetString() == item).GetProperty("quantity").GetInt64() == 0, "sale removes inventory");
        hq = Call("headquarters");
        var preview = Call("commander", new { op = "preview_inherit", revision = Revision(hq), commander = receiver, target = donor });
        Check(Call("headquarters").GetRawText() == hq.GetRawText(), "inheritance preview has no side effects");
        hq = Call("commander", new { op = "inherit", revision = Revision(hq), commander = receiver, target = donor });
        Check(hq.GetProperty("roster").GetArrayLength() == 1 && hq.GetProperty("roster")[0].GetProperty("learning").GetRawText() == preview.GetProperty("learning").GetRawText(), "inheritance matches preview and consumes donor");
        Check(!hq.GetProperty("princesses")[1].GetProperty("claimed").GetBoolean(), "consumed princess becomes claimable");
        hq = Call("commander", new { op = "claim", revision = Revision(hq), offer = donorReward });
        Check(hq.GetProperty("roster")[1].GetProperty("id").GetString() != donor, "reclaimed princess has a fresh instance identity");
        long full = hq.GetProperty("roster")[0].GetProperty("fullPrice").GetInt64(), medals = hq.GetProperty("medals").GetInt64();
        hq = Call("commander", new { op = "full", revision = Revision(hq), commander = receiver });
        Check(hq.GetProperty("medals").GetInt64() == medals - full && hq.GetProperty("roster")[0].GetProperty("fullPrice").GetInt64() == 0, "full promotion charges previewed amount and reaches final stages");
        string savedHeadquarters = hq.GetRawText();
        string savedStock = Call("commerce", new { cell = -1 }).GetProperty("offers").GetRawText();
        host = new ClientGame(content, saveRoot);
        Check(Call("headquarters").GetRawText() == savedHeadquarters, "reward claims, consumed donor, equipment and promotions survive host restart");
        Check(Call("commerce", new { cell = -1 }).GetProperty("offers").GetRawText() == savedStock, "headquarters stock survives host restart");
        Call("commander", new { op = "claim", revision = Revision(hq), offer = donorReward }, "account.reward_claimed");
        foreach (var choice in content.Scenarios.Where(s => s.Mode == 2))
        {
            var source = content.Battle(choice.Id);
            string country = source.Factions.First(f => f.Camp != 4).CountryId;
            var view = Call("start", new { scenario = choice.Id, country });
            foreach (var site in source.Sites.Where(s => s.State6 is >= 1 and <= 3))
                Check(view.GetProperty("cells")[site.Cell].GetProperty("facility").GetString() == new[] { "", "trade", "shop", "bar" }[site.State6], "site service derives from original byte");
        }
        var flagView = Call("start", new { scenario = "campaign1_02b" });
        Check(flagView.GetProperty("factions")[0].GetProperty("country").GetString() == "gb"
            && flagView.GetProperty("factions")[0].GetProperty("flag").GetString() == "gbr", "campaign identity and original flag variant stay distinct");
        var marketView = Call("start", new { scenario = "conquest1", country = "rus" });
        int market = marketView.GetProperty("cells").EnumerateArray().Select((c, i) => (c, i)).First(p => p.c.GetProperty("facility").GetString() == "trade" && p.c.GetProperty("owner").GetString() == marketView.GetProperty("playerFaction").GetString()).i;
        var quote = Call("exchange", new { cell = market });
        var funds = quote.GetProperty("resources");
        long oldRevision = Revision(quote);
        quote = Call("exchange", new { cell = market, op = "buy", revision = oldRevision, offer = "0" });
        Check(quote.GetProperty("resources").GetProperty("money").GetInt64() == funds.GetProperty("money").GetInt64() - 30
            && quote.GetProperty("resources").GetProperty("industry").GetInt64() == funds.GetProperty("industry").GetInt64() + 2, "native resource quote and debit agree");
        Call("exchange", new { cell = market, op = "buy", revision = oldRevision, offer = "0" }, "client.stale");
        Check(Revision(Call("action", new { op = "state", revision = oldRevision })) == Revision(quote), "read refresh recovers after transaction");
        Call("exchange", new { cell = market + 1 }, "session.market_access");
        Call("save");
        var savedResources = quote.GetProperty("resources").GetRawText();
        host = new ClientGame(content, saveRoot);
        var loaded = Call("load");
        Check(loaded.GetProperty("factions").EnumerateArray().Single(f => f.GetProperty("id").GetString() == loaded.GetProperty("playerFaction").GetString()).GetProperty("resources").GetRawText() == savedResources, "resource exchange survives host restart and reload");
        return checks;
    }
}
