# 07 迁移计划

状态:草案

策略:**Core 复制保留;中间层(Content、Session、PlayApi)新写;前端先不动;旧代码封存只读。** 每一步都有明确的验收门槛。

## 1. 现状与已完成项

| 项 | 状态 |
|---|---|
| 旧代码封存(标签 `legacy/0.2.12-sealed`) | ✅ 已完成 |
| 分支 `refactor/pack-architecture` | ✅ 已完成 |
| `src/Ew4.Platform.Engine` 原样复制(与旧目录逐文件一致) | ✅ 已完成 |
| 解决方案、`.editorconfig`、`src/`/`tests/` 构建属性 | ✅ 已完成 |
| 架构文档与 ADR | ✅ 本批提交 |
| Docker dev 编译（`scripts/ew4.sh build`） | ✅ SDK 由 `Dockerfile.dev` 镜像固定 |

## 2. 阶段

### P0:规范定稿(当前)

- 评审本目录文档,敲定:`pack.json` 字段、目录布局、第一批 `IContentKind`、`IEngine`/`RuleSet`/`EngineInfo` 字段。
- 把文档里的"草案"接口落成 `src/` 里**只含契约**的项目(接口、记录类型,无实现),通过编译。
- 建立中间层契约项目(`Ew4.Platform.Content`、`Ew4.Platform.Session`、`Ew4.Platform.Runtime` 等);架构测试在首批实现后添加(见 [development/todo.md](../development/todo.md))。
- **验收:** 评审通过;`dotnet build Ew4.sln` 通过;架构测试在 P1 闸门启用时通过。

### P1:Core 门面 + Pack 读取 + 往返验证

- Core:新增 `RuleSet`、`EngineInfo`、`IEngine` 及 `Engine`(只转调现有静态函数)。
- Content:`IPackSource`(目录/zip)、`pack.json` 解析、`IContentKindRegistry`、校验管线(到 `Reference`)。
- Importers:BTL/XML → JSON(复用旧读取逻辑)。
- Exporters:JSON → BTL/XML。
- **验收:**
  - 现有 `Ew4.Verify` 的世界/战斗/AI 检查通过 `IEngine` 重跑,结果与旧版一致。
  - 95 个剧本 BTL 往返字节一致(差异逐条解释);`def_*.xml` 往返语义一致。
  - 用导入器生成 `packs/base/`。

### P2:内容解析与 Builder

- Content:`IContentResolver`、指纹、迁移器框架、`IScenarioBuilder`;`SmokeStart` 校验级别。
- 脚本运行时(原 `ExtensionLoader` + Lua)迁入,改名,语义不变。
- **验收:**
  - `base` 与语料库中每个包通过 **加载 → 校验到 `SmokeStart`**。
  - 对原版剧本,新 Builder 构建的初始 `WorldState` 与旧 `LegacyScenarioHost` 的结果一致(逐字段对比或哈希对比)。

### P3:Session + PlayApi + Host(替换旧 Client/Mods/Web)

- Session(含存档头与读档规则)、Account、PlayApi(保持旧路由与响应格式)。
- Host(Web、Android),`IHost` 实现包安装/激活/删除。
- 前端 `wwwroot` 沿用。
- **验收:**
  - 现有浏览器验证脚本(`scripts/e2e/verify_*.cjs`)中与游玩、存档、Mod 切换相关的通过。
  - 读档拒绝路径(指纹不符、版本不符)有测试。
  - Android 构建通过;真机验证为单独检查项。

### P4:创作层 MVP

- Authoring:Workspace、`IScenarioEditor`、预览(切换到草稿)、导出。
- 编辑器前端(PC 浏览器)。
- **验收:** PC 浏览器完成"新建草稿 → 编辑 → 校验 → 预览试玩 → 导出 → 导入启用"的完整流程,并有一个端到端示例 Mod 文档(PRD 阶段 C)。

### P5:扩展

- 将领/规则编辑、MapPolicy 与地图上传(含 BIN 双向转换)、移动端编辑体验、存档迁移器。
- 各自单独立项。

## 3. 仓库布局（当前）

- **运行时：** `src/`、`src/Ew4.ModChecks`、`install/web/packs`、`install/web/host`。
- **文档：** `docs/import/mod-kit`、`docs/product/mingguo-mod-plan`。
- **`rewrite/` 已删除**；历史对照在 `backup/`（非运行依赖）。
- 原 `Ew4.Verify` 逻辑逐步迁入 `tests/`。

## 4. 风险与应对

| 风险 | 影响 | 应对 |
|---|---|---|
| 往返不一致:BTL 有未理解的段 | 导出器无法逐字节还原 | `extra` 字段原样保留;差异逐条记录;以此作为"尚未理解"的清单 |
| Builder 与旧 `LegacyScenarioHost` 结果不一致 | 玩法回归 | P2 的逐字段对比验收,不通过不进入 P3 |
| 迁移期同时维护两套代码 | 精力分散 | 旧代码只读封存;新代码只进不退 |
| 前端强依赖旧响应格式 | 前端被迫同步改动 | P3 保持旧路由与响应格式,前端改动放到之后 |
| 没有编译环境 | 契约代码无法验证 | P0 的第一件事是安装 .NET 10 SDK 并编译 |

## 5. 完成的定义(每个阶段通用)

1. 对应文档已更新(状态从"草案"改为"已实现"并记录与设计的偏差)。
2. 新增代码有测试;`dotnet build` 与 `dotnet test` 通过。
3. 依赖规则(架构测试)通过。
4. 涉及重大决定的,有 ADR。
