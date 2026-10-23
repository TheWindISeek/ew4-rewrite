namespace Ew4.Platform.Content;

/// <summary>载入阶段：Merge（在 Resolve 内）、校验、指纹，产出 <see cref="ResolvedContent"/>。</summary>
public interface IModSnapshotLoader
{
    ResolvedContent LoadActivatedView(
        IPackSource primary,
        IPackSource? parent,
        ValidationLevel validation = ValidationLevel.Schema);
}
