using System.Collections.Generic;
using Ew4.Interface.Common;

namespace Ew4.Platform.Content;

public sealed record ContentLimits(long MaxBytesPerFile, int MaxFiles);

public sealed record ValidationContext(ResolvedContent Content);

public interface IContentValidator
{
    IReadOnlyList<Diagnostic> Run(ResolvedContent content, ValidationLevel upTo);
}

public interface IContentKind
{
    string Name { get; }

    string PathPattern { get; }

    int CurrentSchema { get; }

    bool AffectsFingerprint { get; }

    ContentLimits Limits { get; }

    object Load(byte[] data, int schema);

    IEnumerable<Diagnostic> Validate(object model, ValidationContext context);
}

public interface IContentKindRegistry
{
    IReadOnlyList<IContentKind> All { get; }

    IContentKind? Match(string logicalPath);
}
