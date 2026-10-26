using Ew4.Interface.Common;

namespace Ew4.Platform.Mod;

/// <summary>Mod 安装/激活/删除规则违反时抛出；<paramref name="Code"/> 形如 <c>mod.parent</c>。</summary>
public sealed class ModError : OperationException
{
    public ModError(string code, string message)
        : base(code, message)
    {
    }
}
