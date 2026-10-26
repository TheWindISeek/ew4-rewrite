using Ew4.Platform.Content;
using Ew4.Platform.Engine;
using Ew4.Platform.Mod.Activation;
using Ew4.Platform.Mod.Catalog;
using Ew4.Platform.Mod.Import;
using Ew4.Platform.Persistence.Import;
using Ew4.Platform.Persistence.Packages;
using Ew4.Platform.Persistence.User;
using Ew4.Platform.Persistence.Workspaces;
using Ew4.Interface.Common;
using Ew4.Interface.Editor;
using Ew4.Interface.Host;
using Ew4.Interface.Mod;
using Ew4.Interface.Play;
using Ew4.Interface.Runtime;
using Ew4.Service.Plans;
using Ew4.Platform.Mod;
using Ew4.Service.Play;
using Microsoft.Extensions.DependencyInjection;

namespace Ew4.Service.Host;

public static class ServiceRegistration
{
    public static IServiceCollection AddEw4GameHost(this IServiceCollection services, Ew4HostLaunchOptions launch) =>
        AddEw4GameHost(
            services,
            new Ew4InstallOptions
            {
                Install = Ew4HostLaunch.ToInstallLayout(launch),
                RepositoryRoot = launch.RepositoryRoot,
            });

    public static IServiceCollection AddEw4GameHost(this IServiceCollection services, Ew4InstallOptions options)
    {
        services.AddSingleton(options);

        services.AddSingleton<IEngine>(_ => new global::Ew4.Platform.Engine.Engine());
        services.AddSingleton<IScenarioBuilder, ScenarioJsonBuilder>();
        services.AddSingleton<IContentKindRegistry>(_ => ContentKindRegistry.CreateDefault());
        services.AddSingleton(sp =>
            new ContentResolver(sp.GetRequiredService<IContentKindRegistry>(), sp.GetRequiredService<IEngine>()));
        services.AddSingleton<ModSnapshotLoader>(sp =>
            HostAppFactory.CreateSnapshotLoader(
                sp.GetRequiredService<IEngine>(),
                sp.GetRequiredService<IScenarioBuilder>()));
        services.AddSingleton<IModSnapshotLoader>(sp => sp.GetRequiredService<ModSnapshotLoader>());
        services.AddSingleton(_ => new FileModRepository(options.ModsDirectory));
        services.AddSingleton(_ => new WorkspaceStore(options.WorkspacesDirectory));
        services.AddSingleton(_ => new ModImportStagingStore(options.PendingImportsDirectory));
        services.AddSingleton(sp =>
            new ContentValidator(
                sp.GetRequiredService<IContentKindRegistry>(),
                sp.GetRequiredService<IEngine>(),
                sp.GetRequiredService<IScenarioBuilder>()));
        services.AddSingleton<IContentValidator>(sp => sp.GetRequiredService<ContentValidator>());

        services.AddEw4EditorPlatform();

        services.AddSingleton<PlayHost>();
        services.AddSingleton<IPlayFacade>(sp => sp.GetRequiredService<PlayHost>());
        services.AddSingleton<IHostActivationListener>(sp => sp.GetRequiredService<PlayHost>());
        services.AddSingleton<HostApp>(sp =>
        {
            var engine = sp.GetRequiredService<IEngine>();
            var scenarioBuilder = sp.GetRequiredService<IScenarioBuilder>();
            var listeners = sp.GetServices<IHostActivationListener>();
            var app = new HostApp(
                sp.GetRequiredService<FileModRepository>(),
                sp.GetRequiredService<IModSnapshotLoader>(),
                new ActiveModStore(options.StateDirectory),
                engine,
                scenarioBuilder,
                options.StateDirectory,
                listeners);
            try
            {
                app.ActivateStoredOrDefault();
            }
            catch (OperationException)
            {
                app.Activate(ModId.Base);
            }

            return app;
        });
        services.AddSingleton<IRuntimeHost>(sp => sp.GetRequiredService<HostApp>());
        services.AddSingleton<IModHost>(sp => sp.GetRequiredService<HostApp>());
        services.AddSingleton<IContentBindingSource>(sp => sp.GetRequiredService<HostApp>());
        services.AddEw4CampaignEditorAdapter();
        services.AddSingleton(sp =>
            new ModCatalogService(
                sp.GetRequiredService<IModHost>(),
                sp.GetRequiredService<FileModRepository>()));
        services.AddSingleton(sp =>
            new ModImportService(
                sp.GetRequiredService<IModHost>(),
                sp.GetRequiredService<ModImportStagingStore>()));

        services.AddSingleton<PreviewBindingOperations>();
        services.AddSingleton<IPreviewBindingOperations>(sp => sp.GetRequiredService<PreviewBindingOperations>());
        services.AddSingleton<EditorPlans>();
        services.AddSingleton<PlayPlans>();
        services.AddSingleton<ModPlans>();
        services.AddSingleton<PlanRegistry>(sp =>
        {
            var registry = new PlanRegistry();
            sp.GetRequiredService<EditorPlans>().Register(registry);
            sp.GetRequiredService<PlayPlans>().Register(registry);
            sp.GetRequiredService<ModPlans>().Register(registry);
            registry.Validate();
            return registry;
        });
        services.AddSingleton<PlanRunner>();
        services.AddSingleton<IModOperations, ModOps>();
        services.AddSingleton<GameWebPaths>();
        services.AddSingleton<IGameWebPaths>(sp => sp.GetRequiredService<GameWebPaths>());
        services.AddSingleton<PlayStaticAssetService>();
        services.AddSingleton<IGameOperations, GameOperationExecutor>();

        return services;
    }
}
