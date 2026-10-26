using Ew4.Platform.Persistence.Io;

namespace Ew4.Platform.Persistence.Import;

/// <summary>Mod 导入待处理区（pending）上的移动与删除。</summary>
public sealed class ModImportStagingStore
{
    private readonly string _root;

    public ModImportStagingStore(string pendingImportsDirectory)
    {
        _root = PhysicalFiles.GetFullPath(pendingImportsDirectory);
        PhysicalFiles.EnsureDirectory(_root);
    }

    public string Root => _root;

    public string StageMoveFromUpload(string sourcePath, string stagedFileName)
    {
        string target = PhysicalFiles.Combine(_root, stagedFileName);
        PhysicalFiles.Move(sourcePath, target);
        return target;
    }

    public void TryDelete(string path) => PhysicalFiles.TryDeleteFile(path);
}
