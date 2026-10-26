---
name: ew4-extend
description: >-
  Add a feature to EW4 without disturbing existing code: new play/editor route, Step, optional Step (audio, effects), new UI string or language, new mod resource. Use when adding or changing any API, game flow, UI text, or content data.
---

# EW4 加功能

先读冻结约束：[docs/arch/frozen/](../../../docs/arch/frozen/README.md)（尤其 `extension-model.md`）。细节与现状：[docs/arch/operation-plans.md](../../../docs/arch/operation-plans.md)。**冻结文档与 `tests/Ew4.Test.Architecture` 不能改。**

## 判断放哪里

| 需求 | 做法 |
|------|------|
| 新数据/数值/文案 | 放 mod 资源（`mods/<id>/`），Content 读取；不写进代码 |
| 新接口/能力 | 新 Step + 在 `Ew4.Service/Plans/*` 用 `PlanBuilder` 登记一个 Plan |
| 给已有流程加附加效果（音乐、提示） | 在该 Plan **末尾追加可选 Step**，下游用 `OptionalReads`/`TryGet` |
| 新语言/新界面文字 | `assets/client/lang/<code>.json` + 前端 `ew4t(key, fallbackZh)` |
| 新规则 | `Ew4.Platform.Engine`（无 IO）；参数来自 Content |

## 步骤

1. 在 `src/Ew4.Service/Plans/` 找同域的 Plans 文件（`PlayPlans`、`EditorPlans`、`ModPlans`、`MusicPlan`、`BodyPlan` 等），照现有写法。
2. 写 Step：声明 `StepSpec`（Reads/Writes/OptionalReads），只读写自己声明的键。
3. Play 路由：route 名 `play.<route>`，资源键 `session`；可预期失败抛 `PlayFailureException`，Plan 上用 `OnFailure`。非 `PlayRoutes.All` 的路由（如 `music`/`language`）通过 `PlayDescriptors.Describe` 的 ContentRoutes 暴露。
4. 响应：新终端用槽位（`view`/`effects`/`notices`/`diagnostics`）；不要改已迁移终端的 JSON 形状。
5. 测试：`tests/Ew4.Test.Service`（或对应模块的测试项目）加用例；注册表校验失败 = 设计错，不要放宽校验。
6. 验证：

```bash
scripts/ew4.sh build -c Release
scripts/ew4.sh test -c Release
```

7. 哈希用 `Ew4.Interface.Common.Sha256Hash`；磁盘只经 Persistence。
8. 更新对应的**非冻结** arch/plan 文档（`docs/arch/*.md`、`docs/task/plan/*`）。

## 多语言速查

- 回退链：当前语言 → zh-Hans 覆盖表 → 原版 `stringtable_cn.ini` → key。
- 切换：`play.language`（set 后前端整页刷新）；用户设置 `<user>/settings.json`。
- 详见 [docs/arch/localization.md](../../../docs/arch/localization.md)。

## 发现冲突时

需要改 PlanRunner/PlanContext/注册表语义、依赖图、分层、Android 运行时选择：这是改架构，**停下问维护者**。
