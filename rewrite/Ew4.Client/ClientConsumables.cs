using Ew4.Core;

namespace Ew4.Client;

public sealed partial class ClientGame
{
    private object Consumables(ClientCommand command)
    {
        var current = Require(); var session = current.Session;
        var unit = session.World.Units.SingleOrDefault(u => u.Id == command.Unit);
        if (unit == null || unit.Owner != session.PlayerFaction)
            throw new RuleViolation("item.target", "请选择己方部队");
        return new { session.Revision, unit = unit.Id, items = content.ItemDefinitions.Select(e =>
        {
            var item = content.Catalogs.Items.Items[(int)e.Attribute("id")!].Equipment;
            string name = (string)e.Attribute("name")!;
            string? reason = ConsumableItems.UnavailableReason(session, unit.Id, item, current.Rules.Training);
            return new { id = item.Id, name = content.Label("name_" + name), image = name + ".png",
                description = content.Label("desc_" + name), item.Consumable,
                quantity = session.Account.Inventory.GetValueOrDefault(item.Id), available = reason == null, reason };
        }).Where(i => i.Consumable).ToArray() };
    }
}
