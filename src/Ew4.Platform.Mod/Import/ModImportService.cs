using System.Collections.Concurrent;
using Ew4.Platform.Mod.Activation;
using Ew4.Platform.Persistence.Import;
using Ew4.Platform.Persistence.Packages;

namespace Ew4.Platform.Mod.Import;

public sealed class ModImportService
{
    private readonly ConcurrentDictionary<string, PendingImport> _pending = new(StringComparer.Ordinal);
    private readonly IModHost _host;
    private readonly ModImportStagingStore _staging;

    public ModImportService(IModHost host, ModImportStagingStore staging)
    {
        _host = host;
        _staging = staging;
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

        string target = _staging.StageMoveFromUpload(sourcePath, token + extension);

        ModArchiveProbe.EnsureContentModArchive(target);
        var source = new ZipModSource(target);
        _pending[token] = new PendingImport(target, DateTimeOffset.UtcNow.AddHours(1));
        return new PreparedModImport(token, new ModImportIdentity(source.Manifest.Id, source.Manifest.Name));
    }

    public void Discard(string? token)
    {
        if (token == null || !_pending.TryRemove(token, out PendingImport? pending))
        {
            return;
        }

        _staging.TryDelete(pending.Path);
    }

    public void Commit(string token, string id, string name)
    {
        if (!_pending.TryRemove(token, out PendingImport? pending))
        {
            throw new InvalidDataException("请先选择文件");
        }

        if (pending.Expires < DateTimeOffset.UtcNow)
        {
            _staging.TryDelete(pending.Path);
            throw new InvalidDataException("文件选择已过期，请重新选择");
        }

        id = ModId.Validate(id);
        if (string.IsNullOrWhiteSpace(name) || name.Length > 80 || name.Any(char.IsControl))
        {
            _staging.TryDelete(pending.Path);
            throw new InvalidDataException("名称须为1至80个可见字符");
        }

        try
        {
            _host.InstallFromArchive(pending.Path, id);
            _host.Activate(id);
        }
        finally
        {
            _staging.TryDelete(pending.Path);
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

    private sealed record PendingImport(string Path, DateTimeOffset Expires);
}
