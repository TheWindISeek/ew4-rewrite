using System.Collections.Concurrent;
using Ew4.Platform.Content.CatalogLoad;

namespace Ew4.Platform.Content;

public static class ModRuntimeBootstrapCache
{
    private static readonly ConcurrentDictionary<string, ModRuntimeBootstrap> Cache = new(StringComparer.Ordinal);

    internal static ModRuntimeBootstrap Get(ResolvedContent content) =>
        Cache.GetOrAdd(content.Fingerprint, _ => new ModRuntimeBootstrap(content.Source));
}
