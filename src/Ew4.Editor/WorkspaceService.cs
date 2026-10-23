using System.Text.Json;
using Ew4.Platform.Content;

namespace Ew4.Editor;

public sealed class WorkspaceService : IWorkspaceService
{
    private readonly string _workspacesRoot;
    private readonly FileModRepository _packs;
    private readonly IContentKindRegistry _registry;
    private readonly IModSnapshotLoader _snapshotLoader;
    private readonly IContentValidator _validator;
    private readonly ZipModExporter _exporter = new();

    public WorkspaceService(
        string workspacesRoot,
        FileModRepository packs,
        IContentKindRegistry registry,
        IModSnapshotLoader snapshotLoader,
        IContentValidator validator)
    {
        _workspacesRoot = Path.GetFullPath(workspacesRoot);
        _packs = packs;
        _registry = registry;
        _snapshotLoader = snapshotLoader;
        _validator = validator;
        Directory.CreateDirectory(_workspacesRoot);
    }

    public string WorkspacesRoot => _workspacesRoot;

    public IReadOnlyList<WorkspaceInfo> List()
    {
        if (!Directory.Exists(_workspacesRoot))
        {
            return [];
        }

        var list = new List<WorkspaceInfo>();
        foreach (string directory in Directory.EnumerateDirectories(_workspacesRoot))
        {
            string manifestPath = Path.Combine(directory, "mod.json");
            if (!File.Exists(manifestPath))
            {
                continue;
            }

            list.Add(ReadInfo(directory));
        }

        return list.OrderBy(w => w.Id, StringComparer.Ordinal).ToList();
    }

    public WorkspaceInfo Create(string fromModId, string newId, string name)
    {
        newId = ModId.Validate(newId);
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ContentError("workspace.name", "Workspace name is required.");
        }

        string workspaceRoot = ResolveWorkspaceRoot(newId);
        if (Directory.Exists(workspaceRoot) && File.Exists(Path.Combine(workspaceRoot, "mod.json")))
        {
            throw new ContentError("workspace.exists", "Workspace already exists: " + newId);
        }

        IModSource fromPack = _packs.Open(fromModId);
        string parentModId = fromPack.Manifest.Parent ?? fromModId;
        Directory.CreateDirectory(workspaceRoot);

        foreach (string path in fromPack.List(""))
        {
            if (string.Equals(path, "mod.json", StringComparison.Ordinal)
                || string.Equals(path, ".workspace.json", StringComparison.Ordinal))
            {
                continue;
            }

            WritePhysicalFile(workspaceRoot, path, fromPack.Read(path));
        }

        WriteModManifest(workspaceRoot, newId, name, parentModId, fromPack.Manifest.Formats);
        var metadata = new WorkspaceMetadata { CreatedAt = DateTimeOffset.UtcNow };
        metadata.Save(workspaceRoot);
        return ReadInfo(workspaceRoot);
    }

    public void Delete(string workspaceId)
    {
        string workspaceRoot = ResolveWorkspaceRoot(workspaceId);
        if (!Directory.Exists(workspaceRoot))
        {
            throw new ContentError("workspace.missing", "Workspace not found: " + workspaceId);
        }

        Directory.Delete(workspaceRoot, recursive: true);
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
        EnsureWorkspaceExists(workspaceRoot);
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

        WritePhysicalFile(workspaceRoot, logicalPath, data);
        return new WriteResult(logicalPath, diagnostics);
    }

    public IReadOnlyList<Diagnostic> Validate(string workspaceId, ValidationLevel upTo)
    {
        string workspaceRoot = ResolveWorkspaceRoot(workspaceId);
        EnsureWorkspaceExists(workspaceRoot);
        ResolvedContent resolved = WorkspaceModSources.Resolve(workspaceRoot, _packs, _snapshotLoader);
        IReadOnlyList<Diagnostic> diagnostics = _validator.Run(resolved, upTo);
        WorkspaceMetadata metadata = WorkspaceMetadata.Load(workspaceRoot);
        metadata.LastValidatedAt = DateTimeOffset.UtcNow;
        metadata.LastValidationLevel = upTo;
        metadata.Save(workspaceRoot);
        return diagnostics;
    }

    public ExportResult Export(string workspaceId, ExportOptions options)
    {
        string workspaceRoot = ResolveWorkspaceRoot(workspaceId);
        EnsureWorkspaceExists(workspaceRoot);
        IReadOnlyList<Diagnostic> diagnostics = Validate(workspaceId, options.ValidationLevel);
        if (diagnostics.Any(d => d.Severity == Severity.Error))
        {
            return new ExportResult(false, null, diagnostics);
        }

        IModSource merged = OpenMergedMod(workspaceId);
        string exportsDir = Path.Combine(workspaceRoot, "exports");
        Directory.CreateDirectory(exportsDir);
        string fileName = string.IsNullOrWhiteSpace(options.FileName)
            ? workspaceId + "-" + DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss") + ".ew4mod"
            : options.FileName;
        if (!fileName.EndsWith(".ew4mod", StringComparison.OrdinalIgnoreCase))
        {
            fileName += ".ew4mod";
        }

        string archivePath = Path.Combine(exportsDir, fileName);
        using (FileStream stream = File.Create(archivePath))
        {
            _exporter.Export(merged, stream);
        }

        WorkspaceMetadata metadata = WorkspaceMetadata.Load(workspaceRoot);
        metadata.Exports.Add(new WorkspaceExportRecord(DateTimeOffset.UtcNow, archivePath, options.ValidationLevel));
        metadata.Save(workspaceRoot);
        return new ExportResult(true, archivePath, diagnostics);
    }

    public IModSource OpenMergedMod(string workspaceId) =>
        WorkspaceModSources.OpenMerged(ResolveWorkspaceRoot(workspaceId), _packs);

    public string ResolveWorkspaceRoot(string workspaceId)
    {
        workspaceId = ModId.Validate(workspaceId);
        return Path.Combine(_workspacesRoot, workspaceId);
    }

    private WorkspaceInfo ReadInfo(string workspaceRoot)
    {
        var source = new DirectoryModSource(workspaceRoot);
        WorkspaceMetadata metadata = WorkspaceMetadata.Load(workspaceRoot);
        string parent = source.Manifest.Parent ?? ModId.Base;
        return new(
            source.Manifest.Id,
            source.Manifest.Name,
            parent,
            metadata.CreatedAt,
            metadata.LastValidatedAt,
            metadata.LastValidationLevel);
    }

    private static void EnsureWorkspaceExists(string workspaceRoot)
    {
        if (!File.Exists(Path.Combine(workspaceRoot, "mod.json")))
        {
            throw new ContentError("workspace.missing", "Workspace not found: " + Path.GetFileName(workspaceRoot));
        }
    }

    private static void WritePhysicalFile(string workspaceRoot, string logicalPath, byte[] data)
    {
        string target = Path.Combine(workspaceRoot, logicalPath.Replace('/', Path.DirectorySeparatorChar));
        string? parent = Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(parent))
        {
            Directory.CreateDirectory(parent);
        }

        File.WriteAllBytes(target, data);
    }

    private static void WriteModManifest(
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
        File.WriteAllText(Path.Combine(workspaceRoot, "mod.json"), json);
    }
}
