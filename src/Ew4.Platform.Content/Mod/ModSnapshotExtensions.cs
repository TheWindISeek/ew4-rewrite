namespace Ew4.Platform.Content;

public static class ModSnapshotExtensions
{
    public static ResolvedContent AsResolvedContent(this IModSnapshot snapshot) =>
        snapshot as ResolvedContent
        ?? throw new InvalidOperationException("Active mod view must be a loaded ResolvedContent snapshot.");

    public static IReadOnlyList<string> ListScenarioIds(this IModSnapshot snapshot) =>
        ModScenarioCatalog.ListIds(snapshot.AsResolvedContent());
}
