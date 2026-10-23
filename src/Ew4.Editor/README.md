# Ew4.Editor

剧本与 Workspace 编辑**工具库**（不引用 Session/Host）。

| 类型 | 名称 |
|------|------|
| Workspace | `IWorkspaceService` / `WorkspaceService` |
| 剧本结构化编辑 | `IScenarioEditor` / `ScenarioEditor` + `ScenarioEditPatch` |
| BTL ↔ JSON | `IScenarioEditToolkit` / `ScenarioEditToolkit.Default` |
| 兼容 | `IScenarioEditService` → 转调 Toolkit |

编解码在 `Ew4.Platform.Content.ScenarioEdit`（`ScenarioBtlCodec`）；本层负责草稿包编排、校验与导出。

**CLI**（`Ew4.Tool workspace` / `scenario`）与 **HTTP**（`Ew4.Backend.Web` `/api/author/*`）为薄封装。
