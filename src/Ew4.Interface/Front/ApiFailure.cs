namespace Ew4.Interface.Front;

/// <summary>标准游玩 API 错误 JSON（<c>POST /api/{route}?q=…</c>）。</summary>
public sealed record ApiFailure(bool Ok, string Code, string Message)
{
    public static ApiFailure Create(string code, string message) => new(false, code, message);
}
