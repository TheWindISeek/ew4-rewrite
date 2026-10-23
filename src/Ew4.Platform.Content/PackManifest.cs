using System.Collections.Generic;

namespace Ew4.Platform.Content;

/// <summary>
/// <c>pack.json</c> 解析结果（契约；字段以 architecture/03 为准）。
/// </summary>
public sealed record PackManifest(
    int Schema,
    string Id,
    string Name,
    string Version,
    string? Parent,
    PackRequirements Requires,
    IReadOnlyDictionary<string, int> Formats);

public sealed record PackRequirements(
    string Engine,
    IReadOnlyList<string> Capabilities);

public sealed record PackInfo(string Id, string Name, string Version, string? Parent);

public sealed record InstallOptions(bool ReplaceExisting);
