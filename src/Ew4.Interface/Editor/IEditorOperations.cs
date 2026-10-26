namespace Ew4.Interface.Editor;

/// <summary>将领表（Runtime→Editor）；经 <see cref="Runtime.IRuntimeGateway.Commander"/> 暴露。方法显式带 <paramref name="workspaceId"/>。</summary>
public interface IEditorOperations
{
    CommanderTableView ListCommanders(string workspaceId);

    CommanderEntry GetCommander(string workspaceId, int legacyId);

    CommanderSkillCatalogView SkillCatalog(string workspaceId);

    EditorOutcome<CommanderEditOutcome> ApplyCommanderEdit(string workspaceId, CommanderEdit edit);
}
