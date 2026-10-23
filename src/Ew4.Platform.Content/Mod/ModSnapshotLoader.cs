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

    public ResolvedContent LoadActivatedView(
        IPackSource primary,
        IPackSource? parent,
        ValidationLevel validation = ValidationLevel.Schema)
    {
        ResolvedContent resolved = _resolver.Resolve(primary, parent);
        foreach (Diagnostic diagnostic in _validator.Run(resolved, validation))
        {
            if (diagnostic.Severity == Severity.Error)
            {
                throw new ContentError(diagnostic.Code, diagnostic.Message);
            }
        }

        return resolved;
    }
}
