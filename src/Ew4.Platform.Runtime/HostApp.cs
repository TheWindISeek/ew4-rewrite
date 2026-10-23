using System;
using System.Collections.Generic;
using System.IO;
using Ew4.Platform.Content;
using Ew4.Platform.Engine;
using Ew4.Platform.Session;

namespace Ew4.Platform.Runtime;

public sealed class HostApp : IHostApp
{
    private readonly FileModRepository _repository;
    private readonly IModSnapshotLoader _snapshotLoader;
    private readonly ActivePackStore _active;
    private readonly IEngine _engine;
    private readonly IScenarioBuilder _builder;
    private readonly string _stateDirectory;
    private readonly IReadOnlyList<IHostActivationListener> _activationListeners;
    private ResolvedContent? _resolved;
    private SessionService? _session;
    private string? _previewPreviousModId;
    private string? _previewWorkspaceId;

    public HostApp(HostPaths paths, IEngine engine, IScenarioBuilder? builder = null, IEnumerable<IHostActivationListener>? activationListeners = null)
        : this(
            new FileModRepository(paths.ModsDirectory),
            new ModSnapshotLoader(
                new ContentResolver(ContentKindRegistry.CreateDefault(), engine),
                new ContentValidator(ContentKindRegistry.CreateDefault(), engine, builder ?? new ScenarioJsonBuilder())),
            new ActivePackStore(paths.StateDirectory),
            engine,
            builder ?? new ScenarioJsonBuilder(),
            paths.StateDirectory,
            activationListeners)
    {
    }

    public HostApp(
        FileModRepository repository,
        IModSnapshotLoader snapshotLoader,
        ActivePackStore active,
        IEngine engine,
        IScenarioBuilder builder,
        string stateDirectory,
        IEnumerable<IHostActivationListener>? activationListeners = null)
    {
        _repository = repository;
        _snapshotLoader = snapshotLoader;
        _active = active;
        _engine = engine;
        _builder = builder;
        _stateDirectory = stateDirectory;
        _activationListeners = activationListeners?.ToArray() ?? Array.Empty<IHostActivationListener>();
    }

    public ResolvedContent? ActiveContent => _resolved;

    public IModSnapshot? ActiveSnapshot => _resolved;

    public SessionService? Session => _session;

    public HostPreviewState PreviewState => new(
        _previewWorkspaceId != null,
        _previewWorkspaceId,
        _previewPreviousModId);

    public ModStatus Status()
    {
        string? active = _active.Read() ?? _resolved?.Manifest.Id;
        return new ModStatus(_repository.List(), active, false);
    }

    public void Activate(string? modId)
    {
        modId = string.IsNullOrWhiteSpace(modId) ? ModId.Base : ModId.Validate(modId);
        IModSource pack = _repository.Open(modId);
        ModInstallGuard.ValidateActivation(pack, _repository, _engine);
        IModSource? parent = ModMerge.OpenParent(pack, _repository);

        ActivateResolved(pack, parent, persistActiveModId: modId, workspacePreviewId: null);
        ClearPreviewState();
    }

    public void EnterWorkspacePreview(string workspaceRoot, string workspaceId)
    {
        workspaceId = ModId.Validate(workspaceId);
        var pack = new DirectoryModSource(Path.GetFullPath(workspaceRoot));
        ModInstallGuard.ValidateActivation(pack, _repository, _engine);
        IModSource? parent = ModMerge.OpenParent(pack, _repository);

        if (_previewWorkspaceId == null)
        {
            _previewPreviousModId = _active.Read() ?? ModId.Base;
        }

        ActivateResolved(pack, parent, persistActiveModId: null, workspacePreviewId: workspaceId);
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

        string restore = _previewPreviousModId ?? ModId.Base;
        ClearPreviewState();
        Activate(restore);
    }

    private void ActivateResolved(IModSource pack, IModSource? parent, string? persistActiveModId, string? workspacePreviewId)
    {
        ResolvedContent resolved = _snapshotLoader.LoadActivatedView(pack, parent);
        _resolved = resolved;
        string saveRoot = SaveDirectoryLayout.Root(_stateDirectory, resolved, workspacePreviewId);
        _session = new SessionService(resolved, _engine, _builder, saveRoot);
        if (persistActiveModId != null)
        {
            _active.Write(persistActiveModId);
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
        _previewPreviousModId = null;
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

        var source = new ZipModSource(archivePath);
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
        id = ModId.Validate(id);
        var source = new ZipModSource(archivePath);
        if (!string.Equals(source.Manifest.Id, id, StringComparison.Ordinal))
        {
            throw new ContentError("mod.import", "Pack id does not match archive manifest.");
        }

        ModInstallGuard.ValidateInstall(source, _repository, _engine);
        _repository.Install(source, new InstallOptions(ReplaceExisting: true));
    }

    public void Remove(string modId)
    {
        _repository.Remove(modId);
        if (string.Equals(_active.Read(), modId, StringComparison.Ordinal))
        {
            Activate(ModId.Base);
        }
    }

    public void ActivateStoredOrDefault()
    {
        string? modId = _active.Read();
        Activate(modId);
    }
}

public sealed record HostPaths(string ModsDirectory, string StateDirectory);

public sealed record HostPreviewState(bool Active, string? WorkspaceId, string? PreviousModId);
