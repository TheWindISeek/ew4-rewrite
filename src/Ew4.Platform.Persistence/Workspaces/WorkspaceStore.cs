using System;
using System.Collections.Generic;
using System.IO;
using Ew4.Platform.Persistence.Io;
using Ew4.Platform.Persistence.Packages;

namespace Ew4.Platform.Persistence.Workspaces;

/// <summary>创作工作区根目录下的磁盘布局（<c>EW4_HOME/user/workspaces</c> 等）。</summary>
public sealed class WorkspaceStore
{
    private readonly string _root;

    public WorkspaceStore(string workspacesRoot)
    {
        _root = PhysicalFiles.GetFullPath(workspacesRoot);
        PhysicalFiles.EnsureDirectory(_root);
    }

    public string Root => _root;

    public string ResolveWorkspaceDirectory(string workspaceId)
    {
        workspaceId = ModId.Validate(workspaceId);
        return PhysicalFiles.Combine(_root, workspaceId);
    }

    public IReadOnlyList<string> ListWorkspaceDirectoryPaths()
    {
        var list = new List<string>();
        foreach (string directory in PhysicalFiles.EnumerateDirectories(_root))
        {
            string manifestPath = PhysicalFiles.Combine(directory, ModManifestPaths.FileName);
            if (!PhysicalFiles.FileExists(manifestPath))
            {
                continue;
            }

            list.Add(directory);
        }

        return list;
    }

    public bool WorkspaceManifestExists(string workspaceDirectory) =>
        PhysicalFiles.FileExists(PhysicalFiles.Combine(workspaceDirectory, ModManifestPaths.FileName));

    public void EnsureWorkspaceDirectory(string workspaceDirectory) =>
        PhysicalFiles.EnsureDirectory(workspaceDirectory);

    public void DeleteWorkspace(string workspaceId) =>
        PhysicalFiles.DeleteDirectoryRecursive(ResolveWorkspaceDirectory(workspaceId));

    public void WriteWorkspaceFile(string workspaceDirectory, string logicalPath, byte[] data)
    {
        string target = PhysicalFiles.Combine(
            workspaceDirectory,
            logicalPath.Replace('/', Path.DirectorySeparatorChar));
        PhysicalFiles.WriteAllBytes(target, data);
    }

    public string ExportsDirectory(string workspaceDirectory) =>
        PhysicalFiles.Combine(workspaceDirectory, "exports");

    public void EnsureExportsDirectory(string workspaceDirectory) =>
        PhysicalFiles.EnsureDirectory(ExportsDirectory(workspaceDirectory));

    public FileStream CreateExportArchive(string workspaceDirectory, string fileName) =>
        PhysicalFiles.CreateWriteStream(PhysicalFiles.Combine(ExportsDirectory(workspaceDirectory), fileName));

    public string ExportArchivePath(string workspaceDirectory, string fileName) =>
        PhysicalFiles.Combine(ExportsDirectory(workspaceDirectory), fileName);

    public bool WorkspaceDirectoryExists(string workspaceId) =>
        PhysicalFiles.DirectoryExists(ResolveWorkspaceDirectory(workspaceId));

    public bool WorkspaceExists(string workspaceId) =>
        WorkspaceManifestExists(ResolveWorkspaceDirectory(workspaceId));

    public void WriteModManifestJson(string workspaceDirectory, string json) =>
        PhysicalFiles.WriteAllText(
            PhysicalFiles.Combine(workspaceDirectory, ModManifestPaths.FileName),
            json);

    public const string MetadataFileName = ".workspace.json";

    public bool TryReadMetadataJson(string workspaceDirectory, out string json)
    {
        string path = PhysicalFiles.Combine(workspaceDirectory, MetadataFileName);
        if (!PhysicalFiles.FileExists(path))
        {
            json = "";
            return false;
        }

        json = PhysicalFiles.ReadAllText(path);
        return true;
    }

    public void WriteMetadataJson(string workspaceDirectory, string json) =>
        PhysicalFiles.WriteAllText(PhysicalFiles.Combine(workspaceDirectory, MetadataFileName), json);

    public void SaveScenarioDocument(string workspaceDirectory, string scenarioId, byte[] jsonBytes) =>
        WriteWorkspaceFile(workspaceDirectory, "scenarios/" + scenarioId + ".json", jsonBytes);
}
