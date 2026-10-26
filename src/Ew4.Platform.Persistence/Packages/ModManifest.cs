using Ew4.Platform.Persistence;
using System.Collections.Generic;

namespace Ew4.Platform.Persistence.Packages;

/// <summary>
/// <c>mod.json</c> 解析结果（契约；见 mods/README.md）。
/// </summary>
public sealed record ModManifest(
    int Schema,
    string Id,
    string Name,
    string Version,
    string? Parent,
    ModRequirements Requires,
    IReadOnlyDictionary<string, int> Formats,
    ModManifestProfile.ModProfile? Mod = null);

public sealed record ModRequirements(
    string Engine,
    IReadOnlyList<string> Capabilities);

public sealed record ModInfo(string Id, string Name, string Version, string? Parent);

public sealed record InstallOptions(bool ReplaceExisting);
