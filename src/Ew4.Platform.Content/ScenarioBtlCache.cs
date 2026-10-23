using System;
using System.Collections.Concurrent;

namespace Ew4.Platform.Content;

/// <summary>
/// 按内容指纹 + 剧本 id 缓存由 JSON 生成的 BTL 字节（默认仅内存）。
/// </summary>
internal static class ScenarioBtlCache
{
    private static readonly ConcurrentDictionary<string, byte[]> Cache = new(StringComparer.Ordinal);

    public static byte[] GetOrAdd(ResolvedContent content, string scenarioId, Func<byte[]> factory)
    {
        string key = content.Fingerprint + "\0" + scenarioId;
        return Cache.GetOrAdd(key, _ => factory());
    }
}
