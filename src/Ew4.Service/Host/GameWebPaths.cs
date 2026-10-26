using Ew4.Interface.Host;

namespace Ew4.Service.Host;

public sealed class GameWebPaths : IGameWebPaths
{
    public GameWebPaths(Ew4InstallOptions options) =>
        WwwRootDirectory = WebHostPaths.ResolveWwwRoot(options.Install, options.RepositoryRoot);

    public string WwwRootDirectory { get; }
}
