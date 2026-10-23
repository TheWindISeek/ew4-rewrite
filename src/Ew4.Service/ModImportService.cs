using System.Collections.Concurrent;
using Ew4.Platform.Content;
using Ew4.Platform.Runtime;

namespace Ew4.Service;

public sealed record ModImportIdentity(string Id, string Name);

public sealed record PreparedModImport(string Token, ModImportIdentity Identity);

public sealed class ModImportService
{
    private readonly ConcurrentDictionary<string, PendingImport> _pending = new(StringComparer.Ordinal);
    private readonly HostApp _host;
    private readonly string _pendingRoot;

    public ModImportService(HostApp host, string pendingImportsDirectory)
    {
        _host = host;
        _pendingRoot = pendingImportsDirectory;
        Directory.CreateDirectory(_pendingRoot);
    }

    public PreparedModImport StageUploadedFile(string sourcePath, string fileName)
    {
        ExpireStale();
        if (_pending.Count >= 4)
        {
            throw new InvalidOperationException("待导入文件过多，请先完成或取消现有导入");
        }

        string token = Guid.NewGuid().ToString("N");
        string extension = Path.GetExtension(fileName);
        if (string.IsNullOrEmpty(extension))
        {
            extension = ".zip";
        }

        string target = Path.Combine(_pendingRoot, token + extension);
        File.Move(sourcePath, target);

        PackArchiveProbe.EnsureContentPackArchive(target);
        var source = new ZipPackSource(target);
        _pending[token] = new PendingImport(target, DateTimeOffset.UtcNow.AddHours(1));
        return new PreparedModImport(token, new ModImportIdentity(source.Manifest.Id, source.Manifest.Name));
    }

    public void Discard(string? token)
    {
        if (token == null || !_pending.TryRemove(token, out PendingImport? pending))
        {
            return;
        }

        TryDelete(pending.Path);
    }

    public void Commit(string token, string id, string name)
    {
        if (!_pending.TryRemove(token, out PendingImport? pending))
        {
            throw new InvalidDataException("请先选择文件");
        }

        if (pending.Expires < DateTimeOffset.UtcNow)
        {
            TryDelete(pending.Path);
            throw new InvalidDataException("文件选择已过期，请重新选择");
        }

        id = PackId.Validate(id);
        if (string.IsNullOrWhiteSpace(name) || name.Length > 80 || name.Any(char.IsControl))
        {
            TryDelete(pending.Path);
            throw new InvalidDataException("名称须为1至80个可见字符");
        }

        try
        {
            _host.InstallFromArchive(pending.Path, id);
            _host.Activate(id);
        }
        finally
        {
            TryDelete(pending.Path);
        }
    }

    private void ExpireStale()
    {
        foreach (string key in _pending.Keys.ToArray())
        {
            if (_pending.TryGetValue(key, out PendingImport? pending) && pending.Expires < DateTimeOffset.UtcNow)
            {
                Discard(key);
            }
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
    }

    private sealed record PendingImport(string Path, DateTimeOffset Expires);
}
