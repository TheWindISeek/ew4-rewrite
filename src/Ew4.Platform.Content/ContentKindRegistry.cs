using System;
using System.Collections.Generic;
using System.Linq;

namespace Ew4.Platform.Content;

public sealed class ContentKindRegistry : IContentKindRegistry
{
    public static ContentKindRegistry CreateDefault() =>
        new(
            new JsonFileContentKind("scenario", "scenarios/*.json", 2, true, new ContentLimits(8 * 1024 * 1024, 512)),
            new JsonFileContentKind("rules", "rules/**/*.json", 1, true, new ContentLimits(4 * 1024 * 1024, 128)),
            new JsonFileContentKind("map", "maps/**/*.json", 1, true, new ContentLimits(32 * 1024 * 1024, 64)),
            new JsonFileContentKind("commander", "commanders/**/*.json", 1, true, new ContentLimits(4 * 1024 * 1024, 2048)),
            new JsonFileContentKind("unit", "units/**/*.json", 1, true, new ContentLimits(4 * 1024 * 1024, 2048)),
            new JsonFileContentKind("country", "countries/**/*.json", 1, true, new ContentLimits(2 * 1024 * 1024, 512)),
            new JsonFileContentKind("text", "text/**/*.json", 1, true, new ContentLimits(4 * 1024 * 1024, 256)),
            new JsonFileContentKind("script", "scripts/**/*", 1, true, new ContentLimits(256 * 1024, 64)),
            new OpaqueFileContentKind("asset", "assets/**", 1, true, new ContentLimits(64 * 1024 * 1024, 4096)),
            new OpaqueFileContentKind("source", "source/**", 1, false, new ContentLimits(64 * 1024 * 1024, 4096)),
            new OpaqueFileContentKind("rewrite", "rewrite/**", 1, true, new ContentLimits(4 * 1024 * 1024, 128)));

    private readonly IReadOnlyList<IContentKind> _kinds;

    public ContentKindRegistry(params IContentKind[] kinds)
    {
        _kinds = kinds.ToList();
    }

    public IReadOnlyList<IContentKind> All => _kinds;

    public IContentKind? Match(string logicalPath)
    {
        logicalPath = ModPath.Normalize(logicalPath);
        foreach (IContentKind kind in _kinds)
        {
            if (GlobMatcher.IsMatch(kind.PathPattern, logicalPath))
            {
                return kind;
            }
        }

        return null;
    }
}
