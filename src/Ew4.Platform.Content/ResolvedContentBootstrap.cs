using Ew4.Platform.Content.Build;

namespace Ew4.Platform.Content;

public static class ResolvedContentBootstrap
{
    public static PackRuntimeBootstrap Get(ResolvedContent content) => PackRuntimeBootstrapCache.Get(content);
}
