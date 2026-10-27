namespace Ew4.Platform.Mod.Catalog;

/// <summary>与 <c>wwwroot/mods.js</c> 约定的 Mod 面板 JSON 形状。</summary>
public static class ModPanelApi
{
    public static object Serialize(ModPanelState state, bool exportApkSupported = true) => new
    {
        active = state.ActiveModId,
        baseRevision = state.ContentFingerprint,
        exportApkSupported,
        mods = state.Mods.Select(m => new
        {
            key = m.Key,
            id = m.Id,
            name = m.Name,
            version = m.Version,
            revision = m.Id,
            logicRevision = m.Version,
            bytes = m.Bytes,
            files = 0,
            warnings = Array.Empty<string>(),
            active = m.Active,
        }).ToArray(),
        import = new
        {
            status = state.Import.Status,
            message = state.Import.Message,
            completed = state.Import.Completed,
            total = state.Import.Total,
        },
    };
}
