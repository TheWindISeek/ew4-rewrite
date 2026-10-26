using Ew4.Interface.Editor;
using Ew4.Platform.Content;
using Ew4.Platform.Editor.Commander;
using Contract = Ew4.Interface.Editor;

namespace Ew4.Platform.Editor.InterfaceAdapters;

public sealed class EditorOperationsAdapter : IEditorOperations
{
    private readonly ICommanderEditor _commander;

    public EditorOperationsAdapter(ICommanderEditor commander) => _commander = commander;

    public Contract.CommanderTableView ListCommanders(string workspaceId) =>
        EditorInterfaceMapper.ToContract(_commander.List(workspaceId));

    public Contract.CommanderEntry GetCommander(string workspaceId, int legacyId) =>
        EditorInterfaceMapper.ToContract(_commander.Get(workspaceId, legacyId));

    public Contract.CommanderSkillCatalogView SkillCatalog(string workspaceId) =>
        EditorInterfaceMapper.ToContract(_commander.SkillCatalog(workspaceId));

    public EditorOutcome<Contract.CommanderEditOutcome> ApplyCommanderEdit(string workspaceId, Contract.CommanderEdit edit)
    {
        try
        {
            CommanderEditResult result = _commander.Apply(workspaceId, EditorInterfaceMapper.ToPlatform(edit));
            return EditorOutcome<Contract.CommanderEditOutcome>.Ok(EditorInterfaceMapper.ToContract(result));
        }
        catch (ContentError error)
        {
            return EditorOutcome<Contract.CommanderEditOutcome>.Fail(error.Code, error.Message);
        }
    }
}
