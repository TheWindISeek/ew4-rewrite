# Ew4.Editor

Mod **创作**工具库（不引用 Session/Host）。分层见 [docs/arch/mod-layers.md](../../docs/arch/mod-layers.md)。

## 目录（目标）

| 路径 | 职责 |
|------|------|
| 顶层 | `IWorkspaceService`、`WorkspaceService`、`WorkspacePackSources` |
| `Commander/` | `def_commander.xml` overlay 编辑（[API 文档](../../docs/arch/editor-commander-api.md)） |
| `Campaign/` | 战役剧本 / BTL（`ScenarioEditor`、`ScenarioEditToolkit` 等） |
| `Conquest/` | 征服剧本（占位，见 `Conquest/README.md`） |

## 已实现

| 类型 | 说明 |
|------|------|
| Workspace | `IWorkspaceService` / `WorkspaceService` |
| 将领 v1 | `ICommanderEditor` / `CommanderEditor` |
| 战役剧本编辑 | `Ew4.Editor.Campaign`：`IScenarioEditor`、`ScenarioEditToolkit` |
| 兼容 | `IScenarioEditService` → 转调 Toolkit |

编解码在 `Ew4.Platform.Content`（**GameDataLoad** / `ScenarioEdit`）；本层负责草稿包编排、校验与导出。

**CLI**（`Ew4.Tool workspace` / `scenario`）与 **HTTP**（`Ew4.Backend.Web` `/api/author/*`）为薄封装。
