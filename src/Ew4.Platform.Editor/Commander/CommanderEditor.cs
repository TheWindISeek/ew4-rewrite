using Ew4.Platform.Content;

namespace Ew4.Platform.Editor.Commander;

public sealed class CommanderEditor : ICommanderEditor
{
    private readonly IWorkspaceService _workspaces;

    public CommanderEditor(IWorkspaceService workspaces)
    {
        _workspaces = workspaces;
    }

    public CommanderTableView List(string workspaceId) =>
        CommanderTableDocument.Parse(ReadEffectiveTable(workspaceId));

    public CommanderEntry Get(string workspaceId, int legacyId)
    {
        CommanderEntry? entry = List(workspaceId).Entries.FirstOrDefault(e => e.LegacyId == legacyId);
        if (entry == null)
        {
            throw new ContentError("commander.missing", "Commander id not found: " + legacyId);
        }

        return entry;
    }

    public CommanderSkillCatalogView SkillCatalog(string workspaceId) =>
        CommanderSkillCatalog.Load(_workspaces.OpenMergedMod(workspaceId));

    public CommanderEditResult Apply(string workspaceId, CommanderEdit edit)
    {
        byte[] current = ReadEffectiveTable(workspaceId);
        byte[] patched = CommanderTableDocument.ApplyEdit(current, edit);
        WriteResult saved = _workspaces.WriteFile(workspaceId, CommanderTablePaths.WorkspaceRelativePath, patched);
        return new CommanderEditResult(edit.LegacyId, saved);
    }

    private byte[] ReadEffectiveTable(string workspaceId)
    {
        IModSource merged = _workspaces.OpenMergedMod(workspaceId);
        Func<string, byte[]> read = ModAssetReader.Open(merged);
        return read(CommanderTablePaths.RuntimeLogicalPath);
    }
}
