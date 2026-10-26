namespace Ew4.Interface.Runtime;

/// <summary>
/// Runtime → Binding 分派：workspace 合并结果预览游玩（改 <see cref="ContentBinding"/>，非 Editor 表编辑）。
/// 对应 <see cref="Operations.EditorOperationKind"/> 的 preview 系列。
/// </summary>
public interface IPreviewBindingOperations
{
    string GetStateJson();

    void Enter(string workspaceId);

    void Refresh(string workspaceId);

    void Exit();
}
