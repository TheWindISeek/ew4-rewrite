using System.Collections.Concurrent;
using Ew4.Platform.Content.Build;

namespace Ew4.Platform.Content;

public static class PackRuntimeBootstrapCache
{
    private static readonly ConcurrentDictionary<string, PackRuntimeBootstrap> Cache = new(StringComparer.Ordinal);

    internal static PackRuntimeBootstrap Get(ResolvedContent content) =>
        Cache.GetOrAdd(content.Fingerprint, _ => new PackRuntimeBootstrap(content.Source));
}
