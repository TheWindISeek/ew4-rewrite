using System;
using System.Collections.Generic;
using System.Linq;

using Ew4.Platform.Engine;

namespace Ew4.Platform.Content;

public sealed record TradePrice(long Medals, int AppliedPercent);
public sealed record ItemTradeDefinition(Equipment Equipment, long Price, bool Unique, long? MaximumOwned = null);
public sealed record ItemTradingRules
{
    public IReadOnlyDictionary<string, ItemTradeDefinition> Definitions { get; init; } = new Dictionary<string, ItemTradeDefinition>();
    public int CommercePercentPerLevel { get; init; } = 4;
    public int SaleBasePercent { get; init; } = 60;
    public int? MaximumCommerceLevel { get; init; } = 5;
    public int? MaximumDistinctItems { get; init; }
    public void Validate()
    {
        if (CommercePercentPerLevel < 0 || SaleBasePercent < 0 || MaximumDistinctItems < 0 || MaximumCommerceLevel < 0
            || Definitions.Any(p => string.IsNullOrWhiteSpace(p.Key) || p.Key != p.Value.Equipment.Id || p.Value.Price < 0 || p.Value.MaximumOwned < 0))
            throw new RuleViolation("trading.configuration", "Invalid item trading configuration");
    }
}

public sealed record ItemTradeChange(PlayerAccount Account, ItemMarket Market, TradePrice Price);

public static class ItemTrading
{
    private static long Percent(long value, int percent) => checked(value / 100 * percent + value % 100 * percent / 100);
    private static int Commerce(int level, ItemTradingRules rules)
    {
        rules.Validate();
        if (level < 0) throw new RuleViolation("trading.commerce", "Commerce level must be nonnegative");
        return checked((rules.MaximumCommerceLevel.HasValue ? Math.Min(level, rules.MaximumCommerceLevel.Value) : level) * rules.CommercePercentPerLevel);
    }
    public static TradePrice PurchasePrice(long price, int commerceLevel, ItemTradingRules rules)
    {
        if (price < 0) throw new RuleViolation("trading.price", "Negative item price");
        int percent = Math.Min(100, Commerce(commerceLevel, rules));
        long discount = Percent(price, percent);
        return new(price - discount, discount == 0 ? 0 : percent);
    }
    public static TradePrice SalePrice(long price, int commerceLevel, ItemTradingRules rules)
    {
        if (price < 0) throw new RuleViolation("trading.price", "Negative item price");
        int percent = Commerce(commerceLevel, rules);
        return new(Percent(price, checked(rules.SaleBasePercent + percent)), percent);
    }
    private static void Ready(PlayerAccount account, long revision, ItemTradingRules rules)
    {
        account.Validate(); rules.Validate();
        if (revision != account.Revision) throw new RuleViolation("account.revision", "Stale account transaction");
        foreach (var held in account.Inventory)
            if (!rules.Definitions.TryGetValue(held.Key, out var definition)
                || definition.Unique && held.Value > 1 || held.Value > definition.MaximumOwned)
                throw new RuleViolation("trading.inventory", "Inventory does not match trading rules");
        if (account.Inventory.Count > rules.MaximumDistinctItems) throw new RuleViolation("trading.capacity", "Inventory exceeds configured capacity");
    }
    private static void ValidateMarket(ItemMarket market, ItemTradingRules rules)
    {
        if (string.IsNullOrWhiteSpace(market.Id) || market.Offers.Any(o => string.IsNullOrWhiteSpace(o.Id) || !rules.Definitions.ContainsKey(o.ItemId) || o.Remaining < 0)
            || market.Offers.Select(o => o.Id).Distinct(StringComparer.Ordinal).Count() != market.Offers.Count)
            throw new RuleViolation("trading.market", "Invalid market identity, stock or item reference");
    }
    public static bool Available(PlayerAccount account, ItemTradeOffer offer, ItemTradingRules rules)
    {
        Ready(account, account.Revision, rules);
        if (!rules.Definitions.TryGetValue(offer.ItemId, out var definition) || offer.Remaining < 0)
            throw new RuleViolation("trading.offer", "Invalid item offer");
        long owned = account.Inventory.TryGetValue(offer.ItemId, out long count) ? count : 0;
        return offer.Remaining != 0 && !(definition.Unique && owned > 0) && owned != long.MaxValue
            && (!definition.MaximumOwned.HasValue || owned < definition.MaximumOwned)
            && (owned > 0 || !rules.MaximumDistinctItems.HasValue || account.Inventory.Count < rules.MaximumDistinctItems);
    }
    public static ItemTradeChange Buy(PlayerAccount account, long expectedRevision, ItemMarket market, string offerId, int commerceLevel, ItemTradingRules rules)
    {
        Ready(account, expectedRevision, rules); ValidateMarket(market, rules);
        var offer = market.Offers.SingleOrDefault(o => o.Id == offerId);
        if (offer == null || !Available(account, offer, rules)) throw new RuleViolation("trading.unavailable", "Item cannot be acquired");
        var price = PurchasePrice(rules.Definitions[offer.ItemId].Price, commerceLevel, rules);
        if (account.Medals < price.Medals) throw new RuleViolation("account.medals", "Insufficient medals");
        var inventory = account.Inventory.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        inventory[offer.ItemId] = checked((inventory.TryGetValue(offer.ItemId, out long owned) ? owned : 0) + 1);
        var next = account.Copy() with { Revision = checked(account.Revision + 1), Medals = account.Medals - price.Medals, Inventory = inventory };
        var stock = new ItemMarket(market.Id, market.Offers.Select(o => o with { Remaining = o.Id == offer.Id && o.Remaining.HasValue ? o.Remaining - 1 : o.Remaining }).ToArray());
        next.Validate();
        return new(next, stock, price);
    }
    public static ItemTradeChange Sell(PlayerAccount account, long expectedRevision, ItemMarket market, string itemId, int commerceLevel,
        IReadOnlyCollection<string> equippedItems, ItemTradingRules rules)
    {
        Ready(account, expectedRevision, rules); ValidateMarket(market, rules);
        if (!account.Inventory.TryGetValue(itemId, out long owned) || !rules.Definitions.TryGetValue(itemId, out var definition)
            || Math.Max(equippedItems.LongCount(id => id == itemId), account.Roster.Sum(c => c.Items.LongCount(id => id == itemId))) >= owned)
            throw new RuleViolation("trading.unavailable", "Item is absent or all copies are equipped");
        var price = SalePrice(definition.Price, commerceLevel, rules);
        var inventory = account.Inventory.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        if (owned == 1) inventory.Remove(itemId); else inventory[itemId] = owned - 1;
        var next = account.Copy() with { Revision = checked(account.Revision + 1), Medals = checked(account.Medals + price.Medals), Inventory = inventory };
        next.Validate();
        return new(next, new(market.Id, market.Offers.Select(o => o with { }).ToArray()), price);
    }
}
