using System;
using Ew4.Platform.Content.Build;

namespace Ew4.Platform.Content;

/// <summary>
/// 将封存运行时路径（<c>original/</c>、<c>data/</c>、<c>client/</c>）映射到 Pack 内资源。
/// </summary>
public static class ModAssetReader
{
    public static Func<string, byte[]> Open(IModSource pack) => ModAssetRead.FromPack(pack);

    public static bool TryResolve(IModSource pack, string runtimePath, out string packPath)
    {
        string? resolved = ModAssetRead.Resolve(pack, runtimePath);
        if (resolved == null)
        {
            packPath = "";
            return false;
        }

        packPath = resolved;
        return true;
    }
}
