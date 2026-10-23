using Ew4.Platform.Engine;

namespace Ew4.Service;

public sealed partial class ClientGame
{
    private object Deployment(ClientCommand command)
    {
        var current = Require();
        if (command.Revision != current.Session.Revision) throw new RuleViolation("client.stale", "State changed; refresh the command");
        var quota = current.Session.RosterDeployments.SingleOrDefault(q => q.Faction == current.Session.PlayerFaction);
        return new { current.Session.Revision, limit = quota?.Limit, used = quota?.CommanderIds.Count ?? 0,
            commanders = current.Session.Account.Roster.Where(c => RosterDeployment.CanDeploy(current.Session, command.Unit!, c.Id, current.Rules.Roster))
                .Select(c => c.Id).ToArray() };
    }
}
