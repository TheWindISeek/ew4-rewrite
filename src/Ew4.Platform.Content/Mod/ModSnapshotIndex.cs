namespace Ew4.Platform.Content;

public static class ModSnapshotIndex
{
    /// <summary>
    /// 为合并后的 <see cref="IModSource"/> 构建逻辑路径 → 指针表（载入期一次性）。
    /// </summary>
    public static IReadOnlyList<ContentRef> Build(IModSource mergedView)
    {
        if (mergedView is not MergedModSource merged || merged.Parent == null)
        {
            return mergedView.List("")
                .OrderBy(p => p, StringComparer.Ordinal)
                .Select(p => new ContentRef(p, ModPackLayer.Primary, p))
                .ToArray();
        }

        var list = new List<ContentRef>();
        foreach (string path in merged.List("").OrderBy(p => p, StringComparer.Ordinal))
        {
            string normalized = ModPath.Normalize(path);
            ModPackLayer layer = merged.Primary.Exists(normalized) ? ModPackLayer.Primary : ModPackLayer.Parent;
            list.Add(new ContentRef(normalized, layer, normalized));
        }

        return list;
    }
}
