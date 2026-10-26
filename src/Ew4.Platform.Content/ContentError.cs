using Ew4.Interface.Common;

namespace Ew4.Platform.Content;

/// <summary>
/// 内容或 Pack 不合法时抛出；<paramref name="Code"/> 形如 <c>pack.invalid</c>。
/// </summary>
public sealed class ContentError : OperationException
{
    public ContentError(string code, string message)
        : base(code, message)
    {
    }
}
