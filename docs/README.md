# EW4 文档

| 你想… | 去读 |
|--------|------|
| 代码分层、Pack、API | **[arch/README.md](arch/README.md)** |
| Mod 快照与两阶段载入 | **[arch/mod-view.md](arch/mod-view.md)** |
| 怎么推进任务、跑门禁 | **[task/todo.md](task/todo.md)** |
| 接下来做什么 | [task/plan/roadmap.md](task/plan/roadmap.md) |
| 配环境、目录 | [development/environment.md](development/environment.md) |
| 已完成的重构 | [task/done/refactor.md](task/done/refactor.md) |
| 修过的 bug | [debug/README.md](debug/README.md) |
| 你的产品笔记 | [product/README.md](product/README.md) |

速查：[library.md](library.md) · 术语：[glossary.md](glossary.md) · 仓库入口：[../README.md](../README.md)

---

## 目录规则

| 目录 | 性质 | 谁改 |
|------|------|------|
| **arch/** | 代码结构与接口说明 | 接口/结构变更时随代码更新 |
| **task/todo.md** | 任务流程与门禁 | 流程变时 |
| **task/plan/** | 未完成需求（可多文件） | 立项 / 完成时 |
| **task/done/** | 已完成，**按类合并** | 结案时写摘要 |
| **debug/** | 问题与原因（用 TEMPLATE） | 排查 / 修复后 |
| **development/** | 稳定手册（环境、布局、规范） | **仅维护者要求时** |
| **product/** | 维护者产品记录 | 维护者 |

**冲突：** `src/` > `arch/` > `task/plan` > `product`。
