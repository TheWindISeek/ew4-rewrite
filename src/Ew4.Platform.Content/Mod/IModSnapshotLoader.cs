namespace Ew4.Platform.Content;

/// <summary>载入阶段：Merge（在 Resolve 内）、校验、指纹，产出 <see cref="ResolvedContent"/>。</summary>
public interface IModSnapshotLoader
{
    /// <summary>Merge、指纹与指针索引；不跑 <see cref="IContentValidator"/>。</summary>
    ResolvedContent ResolveView(IModSource primary, IModSource? parent);

    ResolvedContent LoadActivatedView(
        IModSource primary,
        IModSource? parent,
        ValidationLevel validation = ValidationLevel.Schema);
}
