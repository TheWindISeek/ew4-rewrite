namespace Ew4.Mods;

public sealed record PreparedModImport(string Token, ModImportIdentity Identity);

public sealed partial class ModStore
{
    private sealed record PendingImport(string Path, DateTimeOffset Expires);
    private readonly Dictionary<string, PendingImport> pendingImports = new(StringComparer.Ordinal);

    public PreparedModImport PrepareImport(string path, string fileName)
    {
        var identity = ModIdentityReader.Read(path, fileName);
        lock (gate)
        {
            foreach (var item in pendingImports.Where(p => p.Value.Expires < DateTimeOffset.UtcNow).ToArray()) DiscardPrepared(item.Key);
            if (pendingImports.Count >= 4) throw new InvalidOperationException("待导入文件过多，请先完成或取消现有导入");
            string token = Guid.NewGuid().ToString("N"), target = ModFiles.Under(root, ".pending-" + token);
            File.Move(path, target);
            pendingImports.Add(token, new(target, DateTimeOffset.UtcNow.AddHours(1)));
            return new(token, identity);
        }
    }
    private void DiscardPrepared(string? token)
    {
        lock (gate)
        {
            if (token != null && pendingImports.TryGetValue(token, out var pending))
            { File.Delete(pending.Path); pendingImports.Remove(token); }
        }
    }
    private void ImportPrepared(string? token, string? id, string? name)
    {
        lock (gate)
        {
            if (token == null || !pendingImports.TryGetValue(token, out var pending)) throw new InvalidDataException("请先选择文件");
            if (pending.Expires < DateTimeOffset.UtcNow) { DiscardPrepared(token); throw new InvalidDataException("文件选择已过期，请重新选择"); }
            ModFiles.Identifier(id ?? "");
            if (string.IsNullOrWhiteSpace(name) || name.Length > 80 || name.Any(char.IsControl)) throw new InvalidDataException("名称须为1至80个可见字符");
            BeginImportCore(pending.Path, id!, name.Trim(), deleteSource: true, renamePackage: true);
            pendingImports.Remove(token);
        }
    }
}
