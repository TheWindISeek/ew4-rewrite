using Ew4.Interface.Common;
using Ew4.Interface.Mod;
using Ew4.Platform.Persistence.Packages;

namespace Ew4.Platform.Content;

public sealed class ModSnapshotLoader : IModSnapshotLoader
{
    private readonly IContentResolver _resolver;
    private readonly IContentValidator _validator;

    public ModSnapshotLoader(IContentResolver resolver, IContentValidator validator)
    {
        _resolver = resolver;
        _validator = validator;
    }

    public IModSnapshot ResolveView(IModPackHandle primary, IModPackHandle? parent)
    {
        ResolvedContent resolved = _resolver.Resolve(
            RequireSource(primary),
            parent == null ? null : RequireSource(parent));
        return resolved with { ContentRefs = ModSnapshotIndex.Build(resolved.Source) };
    }

    public IModSnapshot LoadActivatedView(
        IModPackHandle primary,
        IModPackHandle? parent,
        ValidationLevel validation = ValidationLevel.Schema)
    {
        ResolvedContent resolved = (ResolvedContent)ResolveView(primary, parent);
        foreach (Diagnostic diagnostic in _validator.Run(resolved, validation))
        {
            if (diagnostic.Severity == Severity.Error)
            {
                string detail = string.IsNullOrEmpty(diagnostic.Path)
                    ? diagnostic.Message
                    : diagnostic.Path + ": " + diagnostic.Message;
                throw new ContentError(diagnostic.Code, detail);
            }
        }

        return resolved;
    }

    private static IModSource RequireSource(IModPackHandle? handle) =>
        handle as IModSource
        ?? throw new ArgumentException("Pack handle must be an IModSource instance.", nameof(handle));
}
