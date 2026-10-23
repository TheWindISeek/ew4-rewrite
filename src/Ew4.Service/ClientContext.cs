using System;
using Ew4.Platform.Session;

namespace Ew4.Service;

/// <summary>
/// P3 路由处理器的共享上下文（不依赖 Host 程序集）。
/// </summary>
public sealed class ClientContext
{
    public ClientContext(Func<ISessionService> sessionFactory, IAccountService account)
    {
        SessionFactory = sessionFactory;
        Account = account;
    }

    public Func<ISessionService> SessionFactory { get; }

    public IAccountService Account { get; }

    public ISessionService Session =>
        SessionFactory() ?? throw new InvalidOperationException("No active session; activate a pack first.");
}
