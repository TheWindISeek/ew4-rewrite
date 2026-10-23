using System.Collections.Generic;

namespace Ew4.Platform.Engine;

/// <summary>
/// 引擎能力与版本声明，供 Pack 加载前对照。
/// </summary>
public sealed record EngineInfo(
    string EngineVersion,
    int BehaviorVersion,
    IReadOnlyDictionary<string, int> ContentFormats,
    IReadOnlyCollection<string> Capabilities,
    IReadOnlyCollection<string> KnownCompatFlags);
