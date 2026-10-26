# Ew4.Interface

层间契约：不引用其它 `Ew4.*` 项目。

| 区域 | 内容 |
|------|------|
| **Host** | **`IGameOperations.Invoke`**、`OperationPayload`、`OperationResponse` |
| **Operations** | **`Operations/Operations.cs`**（Descriptor、Classifier、Catalog） |
| **Play** | **`IPlayFacade`**（Persistence / Content / Engine / Json）、`PlayRoutes` |
| **Mod** | **`IModOperations`**、**`IModSnapshotLoader`** / **`IModSnapshot`**（`ModContentRef`）；实现与 `ModSnapshotLoader` 在 Content |
| **Editor** | **`IEditorWorkspaceOperations`**（Content 平面）、`IEditorOperations`、`ICampaignEditorOperations` |
| **Runtime**（绑定） | `IPreviewBindingOperations`（`Enter`/`Refresh` 仅 `workspaceId`）、`ContentBinding` |
| **Plans** | `IStep`、`StepSpec`、`PlanKey<T>`、`PlanContext`、`PlanKeys`（见 docs/arch/operation-plans.md） |
| **Common** / **Front** | 校验 DTO、HTTP 错误形状 |

架构三文档：[module-responsibilities.md](../../docs/arch/module-responsibilities.md) · [interface-contracts.md](../../docs/arch/interface-contracts.md) · [operations.md](../../docs/arch/operations.md)
