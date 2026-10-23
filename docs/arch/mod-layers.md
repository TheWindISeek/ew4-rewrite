# Mod 三层架构

状态：**已定稿（术语）**；目录迁移与 HTTP 按任务单推进。  
关联：[mod-view.md](./mod-view.md)（快照与载入/使用）、[editor-commander-api.md](./editor-commander-api.md)（将领创作接口）。

---

## 1. 三层分工

| 层 | 名称 | 程序集 / 目录（目标） | 做什么 |
|----|------|------------------------|--------|
| **① Mod 运行时** | Mod 运行时 | `Ew4.Platform.Content` → `Mod/` | 安装、`parent` 合并、校验、指纹、`IModSnapshot`、逻辑路径 → 物理文件 |
| **② Mod 创作** | Mod 创作 | **`Ew4.Editor`** | Workspace、按模式改差异文件、导出；**不**重复实现表解析管线 |
| **③ 游戏数据载入** | **GameDataLoad** | `Ew4.Platform.Content` → `Build/`、`Original/`、`ScenarioEdit`（codec） | 在快照之上：封存 XML/BTL/ini → catalog、地图、规则表（Play、preview、smoke 共用） |

对外统一说 **Mod**（`mods/`、`mod.json`）；代码中遗留 `Pack*` 类型名可逐步改为 `Mod*`，与上表概念无关。

---

## 2. 两段「载入」（勿混用）

[mod-view.md](./mod-view.md) 的 **激活载入** 属于 **①**：生成 `IModSnapshot`（合并结果 + `revision`）。

**GameDataLoad** 属于 **③**：在已有快照/合并视图上，把 `original/def_commander.xml` 等读成引擎可用的结构（例如 `LegacyPackRuntimeLoader`、`ModRuntimeBootstrap`、`LegacyCatalogCommanderOverlay`）。

```text
安装 / 激活 / workspace Resolve  →  ① ModSnapshot
Play / preview / Validate       →  ③ GameDataLoad（按需）
创作写回磁盘                      →  ② Editor → 再走 ① → ③
```

---

## 3. `Ew4.Editor` 目录规划

顶层：**对外接口 + 跨模式公用**（workspace、合并视图、校验/导出）。

```text
Ew4.Editor/
  IWorkspaceService.cs          WorkspaceService.cs
  WorkspacePackSources.cs       WorkspaceMetadata.cs
  Campaign/                   # 战役剧本（`Ew4.Editor.Campaign`）

  Commander/                    # 将领：def_commander.xml overlay
  Campaign/                     # 战役：scenarios/campaign*.json、BTL（待建）
  Conquest/                     # 征服：scenarios/conquest*.json（占位）
```

- **HTTP**：`Ew4.Backend.Web` 薄封装；将领规划路由见 [editor-commander-api.md](./editor-commander-api.md)。
- **编解码**：复杂 XML/BTL 归 **Content（GameDataLoad / ScenarioEdit）**；Editor 负责 workspace 编排与作者向 DTO。

---

## 4. GameDataLoad（③）代码地图（当前）

| 区域 | 代表类型 | 说明 |
|------|----------|------|
| 路径映射 | `ModAssetRead` / `ModAssetReader` | `original/*` ↔ `assets/tables/*` |
| Catalog 引导 | `ModRuntimeBootstrap` | 从包加载 catalog / 地图 |
| 将领表覆盖 | `LegacyCatalogCommanderOverlay` | overlay `def_commander.xml` 后重建 catalog 片段 |
| 封存解析 | `Original/*`、`LegacyPackRuntimeLoader` | 表与剧本宿主 |
| 剧本 codec | `ScenarioEdit/*` | BTL ↔ JSON（Editor 战役/征服会调用） |

物理文件夹暂可仍为 `Build/`、`Original/`；文档与新人 onboarding 统一用 **GameDataLoad** 指称③。

---

## 5. 原则

1. **创作只在 Editor**；Content 不再新增「作者子系统」。
2. **Play 与 preview 共用 GameDataLoad**，避免「PlayLoad」等仅游玩向命名。
3. **将领 v1**：只改已有将领的 **技能槽（skill1–4 数值 ID）** 与 **star**；不新增技能定义、不新增将领行（见 API 文档）。
