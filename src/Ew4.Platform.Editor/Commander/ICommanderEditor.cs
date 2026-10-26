namespace Ew4.Platform.Editor.Commander;

public interface ICommanderEditor
{
    CommanderTableView List(string workspaceId);

    CommanderEntry Get(string workspaceId, int legacyId);

    CommanderSkillCatalogView SkillCatalog(string workspaceId);

    CommanderEditResult Apply(string workspaceId, CommanderEdit edit);
}
