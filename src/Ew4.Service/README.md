# Ew4.Service

**编排层**：`GameOperationExecutor` 实现 **`IGameOperations.Invoke`** — 内部分类后调用 `IPlayFacade`、`IModOperations`、`IEditorWorkspaceOperations`，或交给 `PlanRunner`（`Plans/`：注册表把 Request 展开成 Step）。

| 位置 | 职责 |
|------|------|
| `Host/` | `ServiceRegistration`、`HostApp`、`EditorWorkspaceOperationsBridge`、`ModOps`、静态 `/content` |
| `Play/` | `PlayHost`、`PlayRouteRegistry`、`PlaySession`（单文件聚合根；**Web 主路径**） |
| `CliEngineGame.cs` | Tool/CLI 薄局内编排（`Catalog` / `Start` / `Apply` / 槽位）；不经 HTTP |
| `ModOps`（`Host/`） | `IModOperations` 桥接 → `Ew4.Platform.Mod` |

注册：`Host/ServiceRegistration.AddEw4GameHost(Ew4InstallOptions)`。安装根来自 `Ew4.Platform.Persistence.Layout.InstallLayout`。
