using Ew4.Editor;
using Ew4.Platform.Content;
using Ew4.Platform.Engine;
using Ew4.Platform.Runtime;
using Ew4.Backend.Web;
using Ew4.Service;
using Microsoft.AspNetCore.Http.Features;

RuntimeRoots roots = RuntimeRoots.Resolve(args);
AuthorHostOptions hostOptions = new()
{
    Install = roots.Install,
    RepositoryRoot = roots.RepositoryRoot,
};

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddSingleton(hostOptions);
builder.Services.AddSingleton<IScenarioEditToolkit>(ScenarioEditToolkit.Default);
builder.Services.AddSingleton<IEngine>(_ => new global::Ew4.Platform.Engine.Engine());
builder.Services.AddSingleton<IScenarioBuilder, ScenarioJsonBuilder>();
builder.Services.AddSingleton<IContentKindRegistry>(_ => ContentKindRegistry.CreateDefault());
builder.Services.AddSingleton(sp =>
    new ContentResolver(sp.GetRequiredService<IContentKindRegistry>(), sp.GetRequiredService<IEngine>()));
builder.Services.AddSingleton<IModSnapshotLoader>(sp =>
    new ModSnapshotLoader(
        sp.GetRequiredService<ContentResolver>(),
        sp.GetRequiredService<ContentValidator>()));
builder.Services.AddSingleton(sp => new FilePackRepository(hostOptions.ModsDirectory));
builder.Services.AddSingleton(sp =>
    new ContentValidator(
        sp.GetRequiredService<IContentKindRegistry>(),
        sp.GetRequiredService<IEngine>(),
        sp.GetRequiredService<IScenarioBuilder>()));
builder.Services.AddSingleton<IContentValidator>(sp => sp.GetRequiredService<ContentValidator>());
builder.Services.AddSingleton<IWorkspaceService>(sp =>
    new WorkspaceService(
        hostOptions.WorkspacesDirectory,
        sp.GetRequiredService<FilePackRepository>(),
        sp.GetRequiredService<IContentKindRegistry>(),
        sp.GetRequiredService<ContentResolver>(),
        sp.GetRequiredService<IContentValidator>()));
builder.Services.AddSingleton<IScenarioEditor>(sp =>
    new ScenarioEditor(
        sp.GetRequiredService<IWorkspaceService>(),
        sp.GetRequiredService<IScenarioEditToolkit>()));
builder.Services.AddSingleton<PlaySessionHost>();
builder.Services.AddSingleton<IHostActivationListener>(sp => sp.GetRequiredService<PlaySessionHost>());
builder.Services.AddSingleton(sp =>
{
    var engine = sp.GetRequiredService<IEngine>();
    var scenarioBuilder = sp.GetRequiredService<IScenarioBuilder>();
    var listeners = sp.GetServices<IHostActivationListener>();
    var app = new HostApp(
        sp.GetRequiredService<FilePackRepository>(),
        sp.GetRequiredService<IModSnapshotLoader>(),
        new ActivePackStore(hostOptions.StateDirectory),
        engine,
        scenarioBuilder,
        hostOptions.StateDirectory,
        listeners);
    try
    {
        app.ActivateStoredOrDefault();
    }
    catch (ContentError)
    {
        app.Activate(PackId.Base);
    }

    return app;
});
builder.Services.AddSingleton(sp =>
    new ModCatalogService(
        sp.GetRequiredService<HostApp>(),
        sp.GetRequiredService<FilePackRepository>(),
        hostOptions.ModsDirectory));
builder.Services.AddSingleton(sp =>
    new ModImportService(
        sp.GetRequiredService<HostApp>(),
        hostOptions.PendingImportsDirectory));
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 1024L * 1024 * 1024);
builder.Services.Configure<FormOptions>(options => options.MultipartBodyLengthLimit = 1024L * 1024 * 1024);

WebApplication app = builder.Build();
app.UsePlayFrontend(hostOptions);
app.MapPlayModApi();
app.MapPlayApi();
app.MapAuthorWorkspaceApi();
app.MapAuthorScenarioApi();
app.Run();
