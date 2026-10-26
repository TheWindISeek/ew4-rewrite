using Ew4.Platform.Content;
using Ew4.Platform.Engine;
using Ew4.Platform.Persistence.Packages;
using Ew4.Platform.Persistence.User;

namespace Ew4.Service.Host;

/// <summary>与 Web <c>ServiceRegistration</c> 共用同一套 Content 栈与 <see cref="HostApp"/> 构造。</summary>
public static class HostAppFactory
{
    public static ModSnapshotLoader CreateSnapshotLoader(IEngine engine, IScenarioBuilder builder)
    {
        IContentKindRegistry registry = ContentKindRegistry.CreateDefault();
        var resolver = new ContentResolver(registry, engine);
        var validator = new ContentValidator(registry, engine, builder);
        return new ModSnapshotLoader(resolver, validator);
    }

    public static HostApp FromPaths(
        HostPaths paths,
        IEngine engine,
        IScenarioBuilder? builder = null,
        IEnumerable<IHostActivationListener>? activationListeners = null)
    {
        builder ??= new ScenarioJsonBuilder();
        return new HostApp(
            new FileModRepository(paths.ModsDirectory),
            CreateSnapshotLoader(engine, builder),
            new ActiveModStore(paths.StateDirectory),
            engine,
            builder,
            paths.StateDirectory,
            activationListeners);
    }
}
