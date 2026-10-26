using Ew4.Interface.Mod;
using Ew4.Platform.Mod.Merge;
using Ew4.Platform.Persistence.Packages;

namespace Ew4.Platform.Mod.Snapshot;

public static class ModSnapshotIndex
{
    public static IReadOnlyList<ModContentRef> Build(IModSource mergedView)
    {
        if (mergedView is not MergedModSource merged || merged.Parent == null)
        {
            return mergedView.List("")
                .OrderBy(p => p, StringComparer.Ordinal)
                .Select(p => new ModContentRef(p, ModContentLayer.Primary, p))
                .ToArray();
        }

        var list = new List<ModContentRef>();
        foreach (string path in merged.List("").OrderBy(p => p, StringComparer.Ordinal))
        {
            string normalized = ModPath.Normalize(path);
            ModContentLayer layer = merged.Primary.Exists(normalized) ? ModContentLayer.Primary : ModContentLayer.Parent;
            list.Add(new ModContentRef(normalized, layer, normalized));
        }

        return list;
    }
}
