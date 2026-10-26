using Ew4.Platform.Mod.Activation;
using Ew4.Platform.Persistence.Packages;

namespace Ew4.Platform.Mod.Catalog;

public sealed class ModCatalogService
{
    private readonly IModHost _host;
    private readonly FileModRepository _packs;

    public ModCatalogService(IModHost host, FileModRepository packs)
    {
        _host = host;
        _packs = packs;
    }

    public ModPanelState GetPanelState()
    {
        string? activeId = _host.ActiveSnapshot?.PackId;
        bool baseActive = string.Equals(activeId, ModId.Base, StringComparison.Ordinal);
        var mods = _packs.List()
            .Where(p => !string.Equals(p.Id, ModId.Base, StringComparison.Ordinal))
            .Select(p => new ModPanelEntry(
                p.Id,
                p.Id,
                p.Name,
                p.Version,
                _packs.MeasureInstalledBytes(p.Id),
                string.Equals(activeId, p.Id, StringComparison.Ordinal)))
            .ToArray();

        return new ModPanelState(
            baseActive ? null : activeId,
            _host.ActiveSnapshot?.Revision ?? "",
            mods,
            new ModPanelImportState("idle", "", 0, 0));
    }

    public void Activate(string? modId) =>
        _host.Activate(string.IsNullOrEmpty(modId) ? ModId.Base : modId);

    public void Remove(string modId) => _host.Remove(modId);
}
