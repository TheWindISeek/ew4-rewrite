using Ew4.Platform.Content.Build;

namespace Ew4.Platform.Content;

public static class ResolvedContentBootstrap
{
    public static ModRuntimeBootstrap Get(ResolvedContent content) => ModRuntimeBootstrapCache.Get(content);
}
