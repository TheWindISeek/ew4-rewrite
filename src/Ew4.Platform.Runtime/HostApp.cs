using System;
using System.Collections.Generic;
using System.IO;
using Ew4.Platform.Content;
using Ew4.Platform.Engine;
using Ew4.Platform.Session;

namespace Ew4.Platform.Runtime;

public sealed class HostApp : IHostApp
{
    private readonly FilePackRepository _repository;
    private readonly ContentResolver _resolver;
    private readonly ContentValidator _validator;
    private readonly ActivePackStore _active;
    private readonly IEngine _engine;
    private readonly IScenarioBuilder _builder;
    private readonly string _stateDirectory;
    private readonly IReadOnlyList<IHostActivationListener> _activationListeners;
    private ResolvedContent? _resolved;
    private SessionService? _session;
    private string? _previewPreviousPackId;
    private string? _previewWorkspaceId;

    public HostApp(HostPaths paths, IEngine engine, IScenarioBuilder? builder = null, IEnumerable<IHostActivationListener>? activationListeners = null)
        : this(
            new FilePackRepository(paths.ModsDirectory),
            new ContentResolver(ContentKindRegistry.CreateDefault(), engine),
            new ContentValidator(ContentKindRegistry.CreateDefault(), engine, builder ?? new ScenarioJsonBuilder()),
            new ActivePackStore(paths.StateDirectory),
            engine,
            builder ?? new ScenarioJsonBuilder(),
            paths.StateDirectory,
            activationListeners)
    {
    }

    public HostApp(
        FilePackRepository repository,
        ContentResolver resolver,
        ContentValidator validator,
        ActivePackStore active,
        IEngine engine,
        IScenarioBuilder builder,
        string stateDirectory,
        IEnumerable<IHostActivationListener>? activationListeners = null)
    {
        _repository = repository;
        _resolver = resolver;
        _validator = validator;
        _active = active;
        _engine = engine;
        _builder = builder;
        _stateDirectory = stateDirectory;
        _activationListeners = activationListeners?.ToArray() ?? Array.Empty<IHostActivationListener>();
    }

    public ResolvedContent? ActiveContent => _resolved;

    public SessionService? Session => _session;

    public HostPreviewState PreviewState => new(
        _previewWorkspaceId != null,
        _previewWorkspaceId,
        _previewPreviousPackId);

    public PackStatus Status()
    {
        string? active = _active.Read() ?? _resolved?.Manifest.Id;
        return new PackStatus(_repository.List(), active, false);
    }

    public void Activate(string? packId)
    {
        packId = string.IsNullOrWhiteSpace(packId) ? PackId.Base : PackId.Validate(packId);
        IPackSource pack = _repository.Open(packId);
        PackInstallGuard.ValidateActivation(pack, _repository, _engine);
        IPackSource? parent = PackMerge.OpenParent(pack, _repository);

        ActivateResolved(pack, parent, persistActivePackId: packId, workspacePreviewId: null);
        ClearPreviewState();
    }

    public void EnterWorkspacePreview(string workspaceRoot, string workspaceId)
    {
        workspaceId = PackId.Validate(workspaceId);
        var pack = new DirectoryPackSource(Path.GetFullPath(workspaceRoot));
        PackInstallGuard.ValidateActivation(pack, _repository, _engine);
        IPackSource? parent = PackMerge.OpenParent(pack, _repository);

        if (_previewWorkspaceId == null)
        {
            _previewPreviousPackId = _active.Read() ?? PackId.Base;
        }

        ActivateResolved(pack, parent, persistActivePackId: null, workspacePreviewId: workspaceId);
        _previewWorkspaceId = workspaceId;
    }

    public void RefreshWorkspacePreview(string workspaceRoot, string workspaceId) =>
        EnterWorkspacePreview(workspaceRoot, workspaceId);

    public void ExitWorkspacePreview()
    {
        if (_previewWorkspaceId == null)
        {
            return;
        }

        string restore = _previewPreviousPackId ?? PackId.Base;
        ClearPreviewState();
        Activate(restore);
    }

    private void ActivateResolved(IPackSource pack, IPackSource? parent, string? persistActivePackId, string? workspacePreviewId)
    {
        ResolvedContent resolved = _resolver.Resolve(pack, parent);
        IReadOnlyList<Diagnostic> diagnostics = _validator.Run(resolved, ValidationLevel.Schema);
        foreach (Diagnostic diagnostic in diagnostics)
        {
            if (diagnostic.Severity == Severity.Error)
            {
                throw new ContentError(diagnostic.Code, diagnostic.Message);
            }
        }

        _resolved = resolved;
        string saveRoot = SaveDirectoryLayout.Root(_stateDirectory, resolved, workspacePreviewId);
        _session = new SessionService(resolved, _engine, _builder, saveRoot);
        if (persistActivePackId != null)
        {
            _active.Write(persistActivePackId);
        }

        var info = new HostActivationInfo(resolved, workspacePreviewId != null, workspacePreviewId, saveRoot);
        foreach (IHostActivationListener listener in _activationListeners)
        {
            listener.OnActivated(info);
        }
    }

    private void ClearPreviewState()
    {
        _previewWorkspaceId = null;
        _previewPreviousPackId = null;
    }

    public ImportToken Prepare(Stream package, string fileName)
    {
        string temp = Path.Combine(Path.GetTempPath(), "ew4-import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        string archivePath = Path.Combine(temp, fileName);
        using (var file = File.Create(archivePath))
        {
            package.CopyTo(file);
        }

        var source = new ZipPackSource(archivePath);
        return new ImportToken(temp, archivePath, source.Manifest.Id, source.Manifest.Name);
    }

    public void Import(ImportToken token, string id, string name)
    {
        InstallFromArchive(token.ArchivePath, id);
        try
        {
            Directory.Delete(token.TempDirectory, recursive: true);
        }
        catch (IOException)
        {
            // Best effort cleanup.
        }
    }

    public void InstallFromArchive(string archivePath, string id)
    {
        id = PackId.Validate(id);
        var source = new ZipPackSource(archivePath);
        if (!string.Equals(source.Manifest.Id, id, StringComparison.Ordinal))
        {
            throw new ContentError("pack.import", "Pack id does not match archive manifest.");
        }

        PackInstallGuard.ValidateInstall(source, _repository, _engine);
        _repository.Install(source, new InstallOptions(ReplaceExisting: true));
    }

    public void Remove(string packId)
    {
        _repository.Remove(packId);
        if (string.Equals(_active.Read(), packId, StringComparison.Ordinal))
        {
            Activate(PackId.Base);
        }
    }

    public void ActivateStoredOrDefault()
    {
        string? packId = _active.Read();
        Activate(packId);
    }
}

public sealed record HostPaths(string ModsDirectory, string StateDirectory)
{
    public string PacksDirectory => ModsDirectory;
}

public sealed record HostPreviewState(bool Active, string? WorkspaceId, string? PreviousPackId);
