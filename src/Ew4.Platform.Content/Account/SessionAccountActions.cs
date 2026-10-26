using Ew4.Platform.Engine;

namespace Ew4.Platform.Content;

/// <summary>
/// 账户/商店/酒馆/编队/消耗品等非战斗事务：输入 <see cref="GameSession"/>，输出新的 <see cref="SessionChange"/>。
/// 不经 <c>IEngine.Apply</c>；世界变更的 enrichment 与落盘由宿主（Service）提交。
/// </summary>
public static class SessionAccountActions
{
    private static void Revision(GameSession state, long expected)
    {
        state.Validate();
        if (state.Revision != expected) throw new RuleViolation("session.revision", "Stale session command");
    }

    public static SessionChange DeployTavern(GameSession state, long expectedRevision, string unitId, string candidateId, TavernRules rules)
    {
        Revision(state, expectedRevision);
        var faction = WorldActions.Ready(state.World, state.World.Revision);
        rules.Assignment.Validate();
        var candidate = state.TavernCandidates.SingleOrDefault(c => c.Id == candidateId);
        var unit = state.World.Units.SingleOrDefault(u => u.Id == unitId);
        if (candidate == null || unit == null || unit.Owner != faction.Id || unit.CommanderId != null || candidate.Cell != unit.Cell
            || candidate.AvailableRound > state.World.Round || state.World.Cells[unit.Cell].Owner != faction.Id
            || !state.World.Cells[unit.Cell].Terrain.Active
            || (rules.Assignment.DeploymentBuilding == BuildingRequirement.Required && state.World.Cells[unit.Cell].BuildingKind == null)
            || (rules.Assignment.DeploymentBuilding == BuildingRequirement.Forbidden && state.World.Cells[unit.Cell].BuildingKind != null)
            || !rules.Templates.TryGetValue(candidate.CommanderId, out var template)
            || template.Id != candidate.CommanderId || template.Withdrawn || !rules.Assignment.Definitions.ContainsKey(candidate.CommanderId))
            throw new RuleViolation("session.tavern_unavailable", "Candidate cannot join this unit at this round and location");
        bool free = !faction.Human && rules.FreeForAi;
        if (!free && faction.Id != state.PlayerFaction)
            throw new RuleViolation("session.account_owner", "Another faction cannot spend this player's account");
        if (!free && (Affordability.Copies(faction.Resources, candidate.Cost) == 0 || state.Account.Medals < candidate.Medals))
            throw new RuleViolation("session.tavern_funds", "Insufficient battlefield resources or account medals");
        var world = state.World with { Commanders = state.World.Commanders.Concat(new[] { template with { Owner = faction.Id } }).ToArray() };
        var assigned = WorldCommanders.Assign(world, world.Revision, unit.Id, candidate.CommanderId, rules.Assignment);
        var cost = free ? new Resources(0, 0, 0) : candidate.Cost;
        long medals = free ? 0 : candidate.Medals;
        var next = state with { Revision = checked(state.Revision + 1), World = assigned.State with
            { Factions = assigned.State.Factions.Select(f => f.Id != faction.Id ? f : f with { Resources = new(f.Resources.Money - cost.Money, f.Resources.Industry - cost.Industry, f.Resources.Food - cost.Food) }).ToArray() },
            Account = state.Account.Copy() with { Medals = state.Account.Medals - medals, Revision = checked(state.Account.Revision + (medals == 0 ? 0 : 1)) },
            TavernCandidates = state.TavernCandidates.Where(c => c.Id != candidate.Id).ToArray() };
        next = next.Copy(); next.Validate();
        return new(next, assigned.Notices.Concat(new[] { new WorldNotice("tavern.hired", candidate.CommanderId, unit.Cell) }).ToArray());
    }
    private static SessionMarket Market(GameSession state, string marketId)
    {
        var faction = WorldActions.Ready(state.World, state.World.Revision);
        var market = state.Markets.SingleOrDefault(m => m.Market.Id == marketId);
        if (market == null || faction.Id != state.PlayerFaction || !faction.Human || market.Cell is int cell
            && (!state.World.Cells[cell].Terrain.Active || state.World.Cells[cell].Owner != faction.Id || state.World.Cells[cell].BuildingKind == null))
            throw new RuleViolation("session.market_access", "Market is not available to this player");
        return market;
    }
    public static int CommerceLevel(GameSession state, SessionMarket market, SessionTradingRules rules)
    {
        if (rules.TitleCommerce.Any(p => p.Key < 0 || p.Value < 0) || rules.CommanderCommerce.Any(p => string.IsNullOrWhiteSpace(p.Key) || p.Value < 0))
            throw new RuleViolation("session.commerce_rules", "Invalid commander commerce lookup");
        if (market.Cell == null) return 0;
        int highest = 0;
        foreach (var unit in state.World.Units.Where(u => u.Cell == market.Cell && u.Owner == state.PlayerFaction && u.CommanderId != null))
        {
            var commander = state.World.Commanders.Single(c => c.Id == unit.CommanderId);
            var owned = state.Account.Roster.SingleOrDefault(c => c.Id == commander.Id);
            int ability = owned != null && owned.Learning.Abilities.TryGetValue("commerce", out int value) ? value
                : rules.CommanderCommerce.TryGetValue(commander.Id, out int business) ? business
                : throw new RuleViolation("session.commerce_definition", "Missing commerce ability for market commander");
            if (!rules.TitleCommerce.TryGetValue(commander.Title.Level, out int title))
                throw new RuleViolation("session.commerce_definition", "Missing title commerce stage");
            highest = Math.Max(highest, Math.Max(ability, title));
        }
        return highest;
    }
    public static SessionChange BuyItem(GameSession state, long expectedRevision, string marketId, string offerId, SessionTradingRules rules)
    {
        Revision(state, expectedRevision); var market = Market(state, marketId);
        var change = ItemTrading.Buy(state.Account, state.Account.Revision, market.Market, offerId, CommerceLevel(state, market, rules), rules.Items);
        var next = state with { Revision = checked(state.Revision + 1), Account = change.Account,
            Markets = state.Markets.Select(m => m.Market.Id == marketId ? m with { Market = change.Market } : m).ToArray() };
        next = next.Copy(); next.Validate();
        return new(next, new[] { new WorldNotice("account.item_bought", offerId, market.Cell ?? -1) });
    }
    public static SessionChange SellItem(GameSession state, long expectedRevision, string marketId, string itemId, SessionTradingRules rules)
    {
        Revision(state, expectedRevision); var market = Market(state, marketId);
        var change = ItemTrading.Sell(state.Account, state.Account.Revision, market.Market, itemId, CommerceLevel(state, market, rules),
            state.Account.Roster.SelectMany(c => c.Items).ToArray(), rules.Items);
        var next = (state with { Revision = checked(state.Revision + 1), Account = change.Account }).Copy(); next.Validate();
        return new(next, new[] { new WorldNotice("account.item_sold", itemId, market.Cell ?? -1) });
    }
    public static SessionChange Inherit(GameSession state, long expectedRevision, string donorId, string receiverId, AccountCommanderRules rules)
    {
        Revision(state, expectedRevision);
        var account = AccountCommanders.Inherit(state.Account, state.Account.Revision, donorId, receiverId, state.World.Commanders.Select(c => c.Id).ToArray(), rules);
        var next = (state with { Revision = checked(state.Revision + 1), Account = account }).Copy(); next.Validate();
        return new(next, new[] { new WorldNotice("account.commander_inherited", receiverId) });
    }
    public static SessionChange Equip(GameSession state, long expectedRevision, string commanderId, string itemId, AccountCommanderRules rules)
        => ChangeEquipment(state, expectedRevision, commanderId, itemId, rules, true);
    public static SessionChange Unequip(GameSession state, long expectedRevision, string commanderId, string itemId, AccountCommanderRules rules)
        => ChangeEquipment(state, expectedRevision, commanderId, itemId, rules, false);
    private static SessionChange ChangeEquipment(GameSession state, long expectedRevision, string commanderId, string itemId, AccountCommanderRules rules, bool equip)
    {
        Revision(state, expectedRevision);
        if (state.World.Commanders.Any(c => c.Id == commanderId))
            throw new RuleViolation("session.commander_locked", "A participating commander's equipment cannot change during this scenario");
        var account = equip ? AccountCommanders.Equip(state.Account, state.Account.Revision, commanderId, itemId, rules)
            : AccountCommanders.Unequip(state.Account, state.Account.Revision, commanderId, itemId, rules);
        var next = (state with { Revision = checked(state.Revision + 1), Account = account }).Copy(); next.Validate();
        return new(next, new[] { new WorldNotice(equip ? "account.item_equipped" : "account.item_unequipped", commanderId) });
    }

    /// <summary>总部商店购买：以账户修订号为并发令牌，不依赖战场会话修订号。</summary>
    public static SessionChange BuyHeadquarters(GameSession session, long expectedAccountRevision, string offerId, ItemTradingRules items)
    {
        if (session.Account.Revision != expectedAccountRevision) throw new RuleViolation("account.revision", "Roster changed; refresh this command");
        ItemMarket market = session.Account.HeadquartersStock
            ?? throw new RuleViolation("session.market_access", "Headquarters shop stock is not available");
        ItemTradeChange trade = ItemTrading.Buy(session.Account, session.Account.Revision, market, offerId, 0, items);
        GameSession next = session with { Revision = checked(session.Revision + 1), Account = trade.Account with { HeadquartersStock = trade.Market } };
        next.Validate();
        return new SessionChange(next, [new WorldNotice("account.item_bought", offerId, -1)]);
    }

    public static SessionChange SellHeadquarters(GameSession session, long expectedAccountRevision, string itemId, ItemTradingRules items)
    {
        if (session.Account.Revision != expectedAccountRevision) throw new RuleViolation("account.revision", "Roster changed; refresh this command");
        ItemMarket market = session.Account.HeadquartersStock
            ?? throw new RuleViolation("session.market_access", "Headquarters shop stock is not available");
        string[] equipped = session.Account.Roster.SelectMany(c => c.Items).ToArray();
        ItemTradeChange trade = ItemTrading.Sell(session.Account, session.Account.Revision, market, itemId, 0, equipped, items);
        GameSession next = session with { Revision = checked(session.Revision + 1), Account = trade.Account with { HeadquartersStock = trade.Market } };
        next.Validate();
        return new SessionChange(next, [new WorldNotice("account.item_sold", itemId, -1)]);
    }
}
