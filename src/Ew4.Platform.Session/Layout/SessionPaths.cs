namespace Ew4.Platform.Session.Layout;

/// <summary>
/// 用户目录下与存档相关的相对分段（位于 <c>install.user</c> 之下）。
/// </summary>
public static class SessionPaths
{
    public static class User
    {
        public const string Saves = "saves";
        public const string Account = "account.json";
        public const string Session = "session.json";
    }
}
