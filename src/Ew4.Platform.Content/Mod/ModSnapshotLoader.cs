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

    public ResolvedContent ResolveView(IModSource primary, IModSource? parent)
    {
        ResolvedContent resolved = _resolver.Resolve(primary, parent);
        return resolved with { ContentRefs = ModSnapshotIndex.Build(resolved.Source) };
    }

    public ResolvedContent LoadActivatedView(
        IModSource primary,
        IModSource? parent,
        ValidationLevel validation = ValidationLevel.Schema)
    {
        ResolvedContent resolved = ResolveView(primary, parent);
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
}
