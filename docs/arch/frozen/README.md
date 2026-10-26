# 冻结架构文档（FROZEN）

> **任何 AI 不得修改本目录下的文件，也不得写出违背它们的代码。**
> 只有维护者在对话里**明确说“修改架构文档/冻结文档”**时才能改；改动要同时更新 `tests/Ew4.Test.Architecture`，并在提交信息里写明。
> 觉得冻结文档“不合理”或“挡路”时：**停下，向维护者说明冲突点并提问**，不要绕过、不要悄悄改文档、不要改测试白名单。

## 文档

| 文件 | 管什么 |
|------|--------|
| [layering.md](layering.md) | 程序集分层、依赖方向、磁盘/规则/HTTP 的边界（有测试锁定） |
| [extension-model.md](extension-model.md) | 加功能的唯一方式：Plan / Step / 路由；响应契约；失败语义 |
| [content-and-mods.md](content-and-mods.md) | 内容包、Mod、安装渠道、存档、多语言的数据规则 |
| [platform-shells.md](platform-shells.md) | Web 与 Android 外壳、运行时选择、导出 APK 的边界 |

## 与其它文档的关系

- 优先级：**frozen/** > 代码 `src/` > 其它 `docs/arch/` > `docs/task/plan` > `docs/product`。
- 其它 `arch/` 文档描述**现状与细节**，可随代码更新；frozen 文档只写**不随功能变化的约束**。
- 只写约束与理由，不写实现细节（细节会变，约束不该变）。

## 守卫

- `tests/Ew4.Test.Architecture`：锁依赖图、Engine/Interface 零 NuGet、Engine 目标框架。`scripts/ew4.sh test` 必跑。
- `.cursor/rules/arch-frozen.mdc`：让 AI 每次会话都能看到这些规则。
- 扩展步骤见 skill `ew4-extend`（`.cursor/skills/ew4-extend/`）；Android 见 `ew4-android`。
