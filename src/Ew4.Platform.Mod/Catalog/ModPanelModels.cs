namespace Ew4.Platform.Mod.Catalog;

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
