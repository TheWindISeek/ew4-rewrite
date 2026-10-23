using Ew4.Platform.Content;
using Ew4.Platform.Runtime;

namespace Ew4.Service;

public sealed record ModPanelEntry(
    string Key,
    string Id,
    string Name,
    string Version,
    long Bytes,
    bool Active);

public sealed record ModPanelImportState(string Status, string Message, int Completed, int Total);

public sealed record ModPanelState(
    string? ActiveModId,
    string ContentFingerprint,
    IReadOnlyList<ModPanelEntry> Mods,
    ModPanelImportState Import);

public sealed class ModCatalogService
{
    private readonly HostApp _host;
    private readonly FilePackRepository _packs;
    private readonly string _packsDirectory;

    public ModCatalogService(HostApp host, FilePackRepository packs, string packsDirectory)
    {
        _host = host;
        _packs = packs;
        _packsDirectory = packsDirectory;
    }

    public ModPanelState GetPanelState()
    {
        string? activeId = _host.ActiveContent?.Manifest.Id;
        bool baseActive = string.Equals(activeId, PackId.Base, StringComparison.Ordinal);
        var mods = _packs.List()
            .Where(p => !string.Equals(p.Id, PackId.Base, StringComparison.Ordinal))
            .Select(p => new ModPanelEntry(
                p.Id,
                p.Id,
                p.Name,
                p.Version,
                MeasurePackBytes(p.Id),
                string.Equals(activeId, p.Id, StringComparison.Ordinal)))
            .ToArray();

        return new ModPanelState(
            baseActive ? null : activeId,
            _host.ActiveContent?.Fingerprint ?? "",
            mods,
            new ModPanelImportState("idle", "", 0, 0));
    }

    public void Activate(string? packId) =>
        _host.Activate(string.IsNullOrEmpty(packId) ? PackId.Base : packId);

    public void Remove(string packId) => _host.Remove(packId);

    private long MeasurePackBytes(string packId)
    {
        string directory = Path.Combine(_packsDirectory, packId);
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        return Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
            .Sum(path => new FileInfo(path).Length);
    }
}
