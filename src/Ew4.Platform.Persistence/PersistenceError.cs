namespace Ew4.Platform.Persistence;

/// <summary>磁盘或包结构不合法时抛出（Mod/Content 可捕获并映射为对外错误码）。</summary>
public sealed class PersistenceError : Exception
{
    public PersistenceError(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
