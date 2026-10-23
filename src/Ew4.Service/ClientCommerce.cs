using Ew4.Platform.Engine;

namespace Ew4.Service;

public sealed partial class ClientGame
{
    private object[] ItemViews(PlayerAccount account) => content.ItemDefinitions.Select(e =>
    {
        int id = (int)e.Attribute("id")!;
        var definition = content.Catalogs.Items.Items[id];
        string key = definition.Equipment.Id, name = (string)e.Attribute("name")!;
        long quantity = account.Inventory.GetValueOrDefault(key);
        long assigned = account.Roster.Sum(c => c.Items.LongCount(item => item == key));
        return (object)new { id = key, name = content.Label("name_" + name), image = name + ".png",
            description = content.Label("desc_" + name), definition.Price, definition.Unique,
            definition.Equipment.Consumable, quantity, available = quantity - assigned };
    }).ToArray();

    private void InstallAccount(PlayerAccount account)
    {
        StoreAccount(account);
        if (game != null) game = game with { Session = game.Session with { Account = account, Revision = checked(game.Session.Revision + 1) } };
    }

    private ItemMarket HeadquartersMarket(bool refresh = false, long expectedRevision = -1)
    {
        var account = Account();
        if (refresh && expectedRevision != account.Revision)
            throw new RuleViolation("account.revision", "Stale account transaction");
        if (!refresh && account.HeadquartersStock != null) return account.HeadquartersStock;
        var previous = account.HeadquartersStock;
        // Native 5d434: five consumables, then two distinct unowned permanent items.
        var consumables = content.Catalogs.Items.Items.Values.Where(d => d.Equipment.Consumable).ToArray();
        var equipment = content.Catalogs.Items.Items.Values.Where(d => !d.Equipment.Consumable
            && !account.Inventory.ContainsKey(d.Equipment.Id)).ToArray();
        Random.Shared.Shuffle(equipment);
        // Prefer equipment outside the last selection when the remaining pool allows it.
        var oldEquipment = previous?.Offers.Select(o => o.ItemId).ToHashSet() ?? [];
        equipment = equipment.OrderBy(d => oldEquipment.Contains(d.Equipment.Id)).ToArray();
        var offers = new List<ItemTradeOffer>();
        for (int i = 0; i < 5 && consumables.Length > 0; i++)
        {
            string slot = "headquarters." + i;
            string? old = previous?.Offers.FirstOrDefault(o => o.Id == slot)?.ItemId;
            var choices = consumables.Length > 1 ? consumables.Where(d => d.Equipment.Id != old).ToArray() : consumables;
            offers.Add(new(slot, choices[Random.Shared.Next(choices.Length)].Equipment.Id, 1));
        }
        offers.AddRange(equipment.Take(2).Select((d, i) => new ItemTradeOffer("headquarters." + (i + 7), d.Equipment.Id)));
        var market = new ItemMarket("headquarters", offers);
        InstallAccount(account.Copy() with { Revision = checked(account.Revision + 1), HeadquartersStock = market });
        return market;
    }

    private object Commerce(ClientCommand command)
    {
        bool headquarters = command.Cell < 0;
        if (command.Op == "refresh" && !headquarters)
            throw new RuleViolation("trading.command", "Only the headquarters shop can refresh its stock");
        ItemMarket market;
        int commerce = 0;
        ItemTradingRules trading;
        if (headquarters)
        {
            market = HeadquartersMarket(command.Op == "refresh", command.Revision);
            trading = content.Catalogs.Items.Rules();
        }
        else
        {
            var current = Require(); var world = current.Session.World;
            ServiceAccess(command.Cell, command.Op == "tavern" ? "bar" : "shop");
            if (command.Op == "tavern") return TavernView(command.Cell);
            var service = current.Session.Markets.SingleOrDefault(m => m.Cell == command.Cell)
                ?? throw new RuleViolation("session.market_access", "No item shop exists at this location");
            market = service.Market;
            commerce = SessionActions.CommerceLevel(current.Session, service, TradingRules());
            trading = TradingRules().Items;
        }
        if (command.Op is "buy" or "sell")
        {
            if (headquarters)
            {
                var account = Account();
                var change = command.Op == "buy"
                    ? ItemTrading.Buy(account, command.Revision, market, command.Offer!, commerce, trading)
                    : ItemTrading.Sell(account, command.Revision, market, command.Item!, commerce,
                        account.Roster.SelectMany(c => c.Items).ToArray(), trading);
                market = change.Market;
                InstallAccount(change.Account with { HeadquartersStock = market });
            }
            else
            {
                var current = Require();
                var change = command.Op == "buy"
                    ? SessionActions.BuyItem(current.Session, command.Revision, market.Id, command.Offer!, TradingRules())
                    : SessionActions.SellItem(current.Session, command.Revision, market.Id, command.Item!, TradingRules());
                CommitSession(change);
                market = change.State.Markets.Single(m => m.Market.Id == market.Id).Market;
            }
        }
        else if (command.Op is not ("" or "refresh")) throw new RuleViolation("trading.command", "Unknown shop operation");
        var owned = Account();
        return new { revision = headquarters ? owned.Revision : Require().Session.Revision, owned.Medals, commerce,
            items = ItemViews(owned), offers = market.Offers.Select(o => new { o.Id, o.ItemId, o.Remaining,
                slot = int.Parse(o.Id.Split('.').Last()),
                price = ItemTrading.PurchasePrice(trading.Definitions[o.ItemId].Price, commerce, trading).Medals,
                available = ItemTrading.Available(owned, o, trading) }),
            sales = owned.Inventory.Keys.Select(id => new { id, price = ItemTrading.SalePrice(trading.Definitions[id].Price, commerce, trading).Medals }) };
    }

    private object TavernView(int cell)
    {
        var current = Require(); var session = current.Session; var world = session.World;
        var unit = world.Units.FirstOrDefault(u => u.Cell == cell && u.Owner == session.PlayerFaction && u.CommanderId == null);
        var sources = content.Battle(world.ScenarioId).Taverns.SelectMany(t => t.Candidates).ToArray();
        var resources = world.Factions.Single(f => f.Id == session.PlayerFaction).Resources;
        return new { session.Revision, world.Round, session.Account.Medals, unit = unit?.Id,
            candidates = session.TavernCandidates.Where(c => c.Cell == cell).Select(c =>
            {
                int index = int.Parse(c.Id.Split('.').Last());
                return new { c.Id, definitionId = content.Catalogs.Runtime.Commanders[sources[index].CommanderId].Definition.Id,
                    c.Cost, c.Medals, c.AvailableRound,
                    available = unit != null && c.AvailableRound <= world.Round && session.Account.Medals >= c.Medals
                        && resources.Money >= c.Cost.Money && resources.Industry >= c.Cost.Industry };
            }) };
    }
}
