using Ew4.Platform.Engine;

namespace Ew4.Platform.Session;

public sealed class AccountService : IAccountService
{
    public AccountView Get() => new(0, new { });

    public AccountView Apply(EngineCommand command) =>
        throw new RuleViolation("account.command", "Account commands are not implemented yet.");
}
