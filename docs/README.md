# EW4 Rewrite 文档

新代码在 `src/`；运行时内容在 `mods/`。**日常开发以根 [README.md](../README.md)、[development/todo.md](development/todo.md)、`scripts/ew4.sh help` 为准。**

速查：[library.md](library.md) · 术语：[glossary.md](glossary.md)

## 开发（优先读这些）

| 文档 | 内容 |
|------|------|
| [development/environment.md](development/environment.md) | Docker、`ew4.sh`、目录、浏览器烟测 |
| [development/build-install-layout.md](development/build-install-layout.md) | `build/` 与 `install/`、`EW4_HOME`、路径类 |
| [development/todo.md](development/todo.md) | 进度与 Agent 约束 |
| [development/conventions.md](development/conventions.md) | 依赖、代码风格、提交 |
| [development/rule-extensions-without-lua.md](development/rule-extensions-without-lua.md) | 声明式 Mod 规则 |

## 架构（设计参考；部分条目仍为「草案」）

| 文档 | 主题 |
|------|------|
| [architecture/01-overview.md](architecture/01-overview.md) | 分层与依赖 |
| [architecture/02-core-contract.md](architecture/02-core-contract.md) | `IEngine` / `RuleSet` |
| [architecture/03-content-and-packs.md](architecture/03-content-and-packs.md) | Pack、校验、指纹 |
| [architecture/04-session-play-host.md](architecture/04-session-play-host.md) | Session、Play API、Host |
| [architecture/05-authoring.md](architecture/05-authoring.md) | 创作与预览 |
| [architecture/06-versioning-compat.md](architecture/06-versioning-compat.md) | 版本与存档兼容 |
| [architecture/07-migration-plan.md](architecture/07-migration-plan.md) | 分阶段迁移（历史计划） |
| [architecture/08-scenario-edit-pipeline.md](architecture/08-scenario-edit-pipeline.md) | BTL ↔ 剧本 JSON |
| [architecture/10-runtime-assets-and-import.md](architecture/10-runtime-assets-and-import.md) | `mods/` / `install/mods/`、zip 导入、离线 Tool |
| [architecture/11-simplification-and-layout.md](architecture/11-simplification-and-layout.md) | 第二阶段方案：做减法、安装布局、包规范（实施中） |

## 产品（可选）

| 文档 | 内容 |
|------|------|
| [product/PRD.md](product/PRD.md) | 产品需求（讨论稿） |
| [product/mingguo-mod-plan/README.md](product/mingguo-mod-plan/README.md) | 民国 Mod 策划（文本） |

## 状态约定

- 架构文内 **草案** 代码块：设计稿，实现以 `src/` 为准。
- 文档与代码冲突时：**以代码为准**，再回头改文档。
