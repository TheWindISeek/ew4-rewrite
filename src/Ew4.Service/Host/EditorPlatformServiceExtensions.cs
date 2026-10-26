using Ew4.Interface.Editor;
using Ew4.Interface.Mod;
using Ew4.Platform.Content;
using Ew4.Platform.Editor;
using Ew4.Platform.Editor.Campaign;
using Ew4.Platform.Editor.Commander;
using Ew4.Platform.Editor.InterfaceAdapters;
using Ew4.Platform.Engine;
using Ew4.Platform.Mod.Activation;
using Ew4.Platform.Mod.Scenario;
using Ew4.Platform.Persistence.Io;
using Ew4.Platform.Persistence.Packages;
using Ew4.Platform.Persistence.Workspaces;
using Microsoft.Extensions.DependencyInjection;

namespace Ew4.Service.Host;

/// <summary>注册 Platform.Editor 实现与 Interface 适配器（组合根经 Runtime 引用 Editor，Service 不直接依赖 Editor 程序集）。</summary>
public static class EditorPlatformServiceExtensions
{
    public static IServiceCollection AddEw4EditorPlatform(this IServiceCollection services)
    {
        services.AddSingleton<IScenarioBtlConverter, ScenarioBtlConverter>();
        services.AddSingleton<MapCatalogProvider>();
        services.AddSingleton<ScenarioJsonStore>(sp =>
            new ScenarioJsonStore(LoosePathStore.Default, sp.GetRequiredService<WorkspaceStore>()));
        services.AddSingleton<IWorkspaceService>(sp =>
            new WorkspaceService(
                sp.GetRequiredService<WorkspaceStore>(),
                sp.GetRequiredService<FileModRepository>(),
                sp.GetRequiredService<IContentKindRegistry>(),
                sp.GetRequiredService<IModSnapshotLoader>(),
                sp.GetRequiredService<IContentValidator>()));
        services.AddSingleton<IScenarioEditor>(sp =>
            new ScenarioEditor(
                sp.GetRequiredService<IWorkspaceService>(),
                sp.GetRequiredService<ScenarioJsonStore>(),
                sp.GetRequiredService<MapCatalogProvider>()));
        services.AddSingleton<ICommanderEditor>(sp =>
            new CommanderEditor(sp.GetRequiredService<IWorkspaceService>()));
        services.AddSingleton<CampaignDocumentService>(sp =>
            new CampaignDocumentService(
                sp.GetRequiredService<IWorkspaceService>(),
                sp.GetRequiredService<ScenarioJsonStore>(),
                sp.GetRequiredService<IScenarioBtlConverter>(),
                sp.GetRequiredService<MapCatalogProvider>(),
                sp.GetRequiredService<IModSnapshotLoader>(),
                sp.GetRequiredService<FileModRepository>(),
                sp.GetRequiredService<IEngine>(),
                sp.GetRequiredService<IScenarioBuilder>()));
        services.AddSingleton<IEditorOperations>(sp =>
            new EditorOperationsAdapter(sp.GetRequiredService<ICommanderEditor>()));
        services.AddSingleton<EditorWorkspaceOperationsBridge>();
        services.AddSingleton<IEditorWorkspaceOperations>(sp => sp.GetRequiredService<EditorWorkspaceOperationsBridge>());
        return services;
    }

    /// <summary>在 <see cref="IModHost"/> 注册之后调用。</summary>
    public static IServiceCollection AddEw4CampaignEditorAdapter(this IServiceCollection services)
    {
        services.AddSingleton<ICampaignEditorOperations>(sp =>
            new CampaignEditorOperationsAdapter(
                sp.GetRequiredService<CampaignDocumentService>(),
                sp.GetRequiredService<IScenarioBtlConverter>(),
                sp.GetRequiredService<IScenarioEditor>()));
        return services;
    }
}
