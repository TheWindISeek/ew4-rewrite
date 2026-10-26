namespace Ew4.Interface.Common;

/// <summary>
/// Invoke 编排层可映射为客户端错误 JSON 的预期失败。
/// Platform 程序集宜抛出本类型或其子类（如内容/Mod 校验失败），而非让 Service 依赖具体程序集异常。
/// </summary>
public class OperationException : Exception
{
    public OperationException(string code, string message)
        : base(message)
    {
        Code = code;
    }

    public string Code { get; }
}
