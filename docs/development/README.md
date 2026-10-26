# 开发手册（稳定）

环境、目录布局、代码规范、Mod 规则扩展。**不含任务清单**（见 [task/todo.md](../task/todo.md)）。

除非维护者明确要求，**不要为顺手改文档而修改本目录**；接口或结构变更应改 [arch/](../arch/module-responsibilities.md)（三文档）。

| 文档 | 内容 |
|------|------|
| [environment.md](./environment.md) | Docker、`ew4.sh`、烟测 |
| [build-install-layout.md](./build-install-layout.md) | `build/`、`install/`、`EW4_HOME` |
| [conventions.md](./conventions.md) | 依赖、风格、提交 |
| [rule-extensions-without-lua.md](./rule-extensions-without-lua.md) | 声明式 Mod 规则 |
