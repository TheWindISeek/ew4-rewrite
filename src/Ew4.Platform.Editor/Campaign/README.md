# 战役创作

Workspace 内 **战役剧本**（`scenarios/campaign*.json`、嵌入 BTL）的结构化编辑。

| 类型 | 说明 |
|------|------|
| `IScenarioEditor` / `ScenarioEditor` | workspace 内 apply patch |
| `IScenarioEditToolkit` / `ScenarioEditToolkit` | BTL ↔ JSON、读写 workspace |
| `ScenarioEditPatch` | 改 `ScenarioDocument.Layers` |
| `IScenarioEditService` | 兼容旧名，转调 Toolkit |

HTTP：`Ew4.Backend.Web` → `/api/editor/workspaces/{id}/campaign/*`（战役剧本 BTL/JSON）。
