using System.IO;
using System.Text.Json;
using Ew4.Interface.Mod;
using Ew4.Platform.Content;
using Ew4.Platform.Editor.Campaign;
using Ew4.Platform.Persistence.Packages;
using Ew4.Platform.Persistence.Workspaces;

namespace Ew4.Platform.Editor;

public sealed class WorkspaceService : IWorkspaceService
{
    private readonly WorkspaceStore _store;
    private readonly FileModRepository _packs;
    private readonly IContentKindRegistry _registry;
    private readonly IModSnapshotLoader _snapshotLoader;
    private readonly IContentValidator _validator;
    private readonly ZipModExporter _exporter = new();

    public WorkspaceService(
        WorkspaceStore store,
        FileModRepository packs,
        IContentKindRegistry registry,
        IModSnapshotLoader snapshotLoader,
        IContentValidator validator)
    {
        _store = store;
        _packs = packs;
        _registry = registry;
        _snapshotLoader = snapshotLoader;
        _validator = validator;
    }

    public string WorkspacesRoot => _store.Root;

    public IReadOnlyList<WorkspaceInfo> List()
    {
        var list = new List<WorkspaceInfo>();
        foreach (string directory in _store.ListWorkspaceDirectoryPaths())
        {
            list.Add(ReadInfo(directory));
        }

        return list.OrderBy(w => w.Id, StringComparer.Ordinal).ToList();
    }

    public WorkspaceInfo Create(string fromModId, string newId, string name, EditorMode mode = EditorMode.BaseTweak)
    {
        newId = ModId.Validate(newId);
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ContentError("workspace.name", "Workspace name is required.");
        }

        string workspaceRoot = ResolveWorkspaceRoot(newId);
        if (_store.WorkspaceDirectoryExists(newId) && _store.WorkspaceManifestExists(workspaceRoot))
        {
            throw new ContentError("workspace.exists", "Workspace already exists: " + newId);
        }

        IModSource fromMod = _packs.Open(fromModId);
        string parentModId = fromMod.Manifest.Parent ?? fromModId;
        _store.EnsureWorkspaceDirectory(workspaceRoot);

        foreach (string path in fromMod.List(""))
        {
            if (string.Equals(path, "mod.json", StringComparison.Ordinal)
                || string.Equals(path, ".workspace.json", StringComparison.Ordinal))
            {
                continue;
            }

            _store.WriteWorkspaceFile(workspaceRoot, path, fromMod.Read(path));
        }

        WriteModManifest(_store, workspaceRoot, newId, name, parentModId, fromMod.Manifest.Formats);
        var metadata = new WorkspaceMetadata { CreatedAt = DateTimeOffset.UtcNow, Mode = EditorModes.Format(mode) };
        metadata.Save(_store, workspaceRoot);
        return ReadInfo(workspaceRoot);
    }

    public void Delete(string workspaceId)
    {
        if (!_store.WorkspaceDirectoryExists(workspaceId))
        {
            throw new ContentError("workspace.missing", "Workspace not found: " + workspaceId);
        }

        _store.DeleteWorkspace(workspaceId);
    }

    public byte[] ReadFile(string workspaceId, string logicalPath)
    {
        logicalPath = ModPath.Normalize(logicalPath);
        IModSource merged = OpenMergedMod(workspaceId);
        if (!merged.Exists(logicalPath))
        {
            throw new ContentError("workspace.file", "File not found in workspace pack: " + logicalPath);
        }

        return merged.Read(logicalPath);
    }

    public WriteResult WriteFile(string workspaceId, string logicalPath, byte[] data)
    {
        logicalPath = ModPath.Normalize(logicalPath);
        if (string.Equals(logicalPath, "mod.json", StringComparison.Ordinal)
            || string.Equals(logicalPath, ".workspace.json", StringComparison.Ordinal))
        {
            throw new ContentError("workspace.path", "Manifest metadata cannot be written through this API.");
        }

        IContentKind? kind = _registry.Match(logicalPath);
        if (kind == null)
        {
            throw new ContentError("workspace.path", "No content kind is registered for path: " + logicalPath);
        }

        if (data.Length > kind.Limits.MaxBytesPerFile)
        {
            throw new ContentError(
                "workspace.size",
                $"File exceeds limit of {kind.Limits.MaxBytesPerFile} bytes.");
        }

        string workspaceRoot = ResolveWorkspaceRoot(workspaceId);
        EnsureWorkspaceExists(workspaceId);
        ResolvedContent resolved = WorkspaceModSources.Resolve(workspaceRoot, _packs, _snapshotLoader);
        if (!resolved.Manifest.Formats.TryGetValue(kind.Name, out int schema))
        {
            throw new ContentError("workspace.format", $"mod.json formats.{kind.Name} is required.");
        }

        object model = kind.Load(data, schema);
        var diagnostics = kind.Validate(model, new ValidationContext(resolved))
            .Select(d => d with { Path = string.IsNullOrEmpty(d.Path) ? logicalPath : d.Path })
            .ToList();
        if (diagnostics.Any(d => d.Severity == Severity.Error))
        {
            throw new ContentError(
                "workspace.validate",
                "Write rejected: " + diagnostics.First(d => d.Severity == Severity.Error).Message);
        }

        _store.WriteWorkspaceFile(workspaceRoot, logicalPath, data);
        return new WriteResult(logicalPath, diagnostics);
    }

    public IReadOnlyList<Diagnostic> Validate(string workspaceId, ValidationLevel upTo)
    {
        string workspaceRoot = ResolveWorkspaceRoot(workspaceId);
        EnsureWorkspaceExists(workspaceId);
        ResolvedContent resolved = WorkspaceModSources.Resolve(workspaceRoot, _packs, _snapshotLoader);
        IReadOnlyList<Diagnostic> diagnostics = _validator.Run(resolved, upTo);
        WorkspaceMetadata metadata = WorkspaceMetadata.Load(_store, workspaceRoot);
        metadata.LastValidatedAt = DateTimeOffset.UtcNow;
        metadata.LastValidationLevel = upTo;
        metadata.Save(_store, workspaceRoot);
        return diagnostics;
    }

    public ExportResult Export(string workspaceId, ExportOptions options)
    {
        string workspaceRoot = ResolveWorkspaceRoot(workspaceId);
        EnsureWorkspaceExists(workspaceId);
        IReadOnlyList<Diagnostic> diagnostics = Validate(workspaceId, options.ValidationLevel);
        if (diagnostics.Any(d => d.Severity == Severity.Error))
        {
            return new ExportResult(false, null, diagnostics);
        }

        IModSource exported = options.Standalone
            ? new ExportModSource(OpenMergedMod(workspaceId), dropParent: true)
            : new ExportModSource(new DirectoryModSource(workspaceRoot), dropParent: false);
        _store.EnsureExportsDirectory(workspaceRoot);
        string fileName = string.IsNullOrWhiteSpace(options.FileName)
            ? workspaceId + "-" + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss") + ".ew4mod"
            : options.FileName;
        if (!fileName.EndsWith(".ew4mod", StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".ew4mod";
        }

        string archivePath = _store.ExportArchivePath(workspaceRoot, fileName);
        using (FileStream stream = _store.CreateExportArchive(workspaceRoot, fileName))
        {
            _exporter.Export(exported, stream);
        }

        WorkspaceMetadata metadata = WorkspaceMetadata.Load(_store, workspaceRoot);
        metadata.Exports.Add(new WorkspaceExportRecord(DateTimeOffset.UtcNow, archivePath, options.ValidationLevel));
        metadata.Save(_store, workspaceRoot);
        return new ExportResult(true, archivePath, diagnostics);
    }

    public IModSource OpenMergedMod(string workspaceId) =>
        WorkspaceModSources.OpenMerged(ResolveWorkspaceRoot(workspaceId), _packs);

    public string ListCampaignsJson(string workspaceId)
    {
        CampaignCatalogGroups groups = CampaignCatalog.ListByMode(OpenMergedMod(workspaceId));
        return JsonSerializer.Serialize(new
        {
            campaign = groups.Campaign,
            conquest = groups.Conquest,
            tutorial = groups.Tutorial,
            other = groups.Other,
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
    }

    public string ResolveWorkspaceRoot(string workspaceId) => _store.ResolveWorkspaceDirectory(workspaceId);

    private WorkspaceInfo ReadInfo(string workspaceRoot)
    {
        var source = new DirectoryModSource(workspaceRoot);
        WorkspaceMetadata metadata = WorkspaceMetadata.Load(_store, workspaceRoot);
        string parent = source.Manifest.Parent ?? ModId.Base;
        return new(
            source.Manifest.Id,
            source.Manifest.Name,
            parent,
            metadata.CreatedAt,
            metadata.LastValidatedAt,
            metadata.LastValidationLevel,
            EditorModes.Format(EditorModes.Parse(metadata.Mode)));
    }

    public EditorMode GetMode(string workspaceId)
    {
        EnsureWorkspaceExists(workspaceId);
        return EditorModes.Parse(WorkspaceMetadata.Load(_store, ResolveWorkspaceRoot(workspaceId)).Mode);
    }

    private void EnsureWorkspaceExists(string workspaceId)
    {
        if (!_store.WorkspaceExists(workspaceId))
        {
            throw new ContentError("workspace.missing", "Workspace not found: " + workspaceId);
        }
    }

    private static void WriteModManifest(
        WorkspaceStore store,
        string workspaceRoot,
        string id,
        string name,
        string parentModId,
        IReadOnlyDictionary<string, int> formats)
    {
        var manifest = new
        {
            schema = 1,
            id,
            name,
            version = "0.0.0",
            parent = parentModId,
            requires = new { engine = ">=0.3.0", capabilities = Array.Empty<string>() },
            formats = formats.ToDictionary(p => p.Key, p => p.Value),
        };

        string json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });
        store.WriteModManifestJson(workspaceRoot, json);
    }
}
