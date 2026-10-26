using Ew4.Interface.Common;

namespace Ew4.Interface.Mod;

/// <summary>载入阶段：Merge（在 Resolve 内）、校验、指纹。实现位于 <c>Ew4.Platform.Content.ModSnapshotLoader</c>。</summary>
public interface IModSnapshotLoader
{
    /// <summary>Merge、指纹与指针索引；不跑内容校验器。</summary>
    IModSnapshot ResolveView(IModPackHandle primary, IModPackHandle? parent);

    IModSnapshot LoadActivatedView(
        IModPackHandle primary,
        IModPackHandle? parent,
        ValidationLevel validation = ValidationLevel.Schema);
}
