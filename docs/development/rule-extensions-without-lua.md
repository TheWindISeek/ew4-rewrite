# 声明式规则扩展(不用 Lua)

状态:已接受（新 Mod 不引入 Lua）

## 原则

1. **原版 `base` 与新 Mod 均不依赖 Lua。**
2. Mod 作者通过 `rewrite/extension.json` 引用 **JSON 定义**(技能、绑定、胜负、剧本事件等);引擎在 **Core** 解释并执行。
3. 当 JSON 表达不了时,**增加 Core 字段与实现**,而不是加 `.lua` 文件。
4. 封存 `0.2.x` 的 `script`/`function` 仅作迁移对照;新代码不为 Mod 新增 Lua 钩子。

## 封存版 Lua 实际在干什么(便于迁移)

| 场景 | 调用位置 | Lua 典型逻辑 | 声明式替代方向 |
|------|----------|--------------|----------------|
| 扩展技能/被动 | `RuleEventBus.Modify` / `Dispatch` | `value * level`、按 category 倍率 | `scaleByLevel`、`whenCategory`、`multiplier` |
| 物品效果 | 同上 + `itemBindings` | 与技能相同 | 同上 |
| 状态 tick | `turn.start` 等 | `value * stacks` | `scaleByStacks` 或固定 `operation` |
| 剧本条件 | `ScenarioRules.Match` `type: script` | 自定义查询 | 新 `ScenarioCondition` 类型或组合条件 |
| 自定义胜负 | `ExtensionVictory` `type: script` | 敌方兵力/目标数阈值 | `enemy_units_remaining` / `enemy_objectives_remaining` + `compare`/`value`；API 2 禁止 `script` |

官方 mod-kit 示例脚本极短(见 `backup/rewrite/mod-kit/examples/**/rewrite/scripts/*.lua`),**应优先用声明式字段覆盖**,不必 1:1 保留 MoonSharp。

## 实现落点(给开发)

| 层 | 文件(起点) |
|----|------------|
| 技能/状态效果 | `Ew4.Platform.Engine/RuleExtensions.cs`、`RuleEventBus`（`scaleBy` / `targetCategory` / `stateKey` / `addStateToValue`） |
| 剧本事件 | `Ew4.Platform.Engine/ScenarioRules.cs` |
| 扩展胜负 | `Ew4.Platform.Engine/ExtensionVictory.cs` |
| Mod 加载 | `Ew4.Platform.Content/Build/ExtensionLoader.cs`(逐步拒绝或转换 `scripts`) |
| Pack 校验 | `ContentValidator`、扩展 schema 版本 |

## 验收建议

- `mods/base` SmokeStart **无** `rewrite/extension.json` → 零脚本。
- 示例 Pack 声明式 JSON 化后,`scripts/ew4.sh validate smoke` 通过。
- 新字段有文档与 schema 版本;行为变更走 `BehaviorVersion`(见 [arch/versioning.md](../arch/versioning.md))。
