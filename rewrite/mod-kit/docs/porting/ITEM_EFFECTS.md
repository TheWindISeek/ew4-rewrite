# 物品与装备效果接口

在 API 2 的 `rewrite/extension.json` 增加 `itemBindings`，物品即可复用将领/兵种的技能 JSON、事件和外置 Lua。示例见[可打包模板](../../examples/item-effects/README.md)。需要包含本接口的新版引擎；旧 0.2.9 及更早发行版不识别该字段。

```json
{
  "schema": 1, "apiVersion": 2,
  "skills": ["rewrite/definitions/item-skills.json"],
  "itemBindings": "rewrite/definitions/item-bindings.json",
  "scripts": ["rewrite/scripts/items.lua"]
}
```

`itemBindings` 指向数组，每个完整物品 ID 只能配置一条。所有引用都采用包内 `rewrite/` 路径，沿用 JSON/Lua 的大小、语法、预算和异常校验。

```json
[
  {"target":"ew4.item.8", "replaceLegacyEffect":true,
   "skills":[{"id":"my.ward","instanceId":"ward","level":2}]},
  {"target":"ew4.item.13", "replaceLegacyEffect":true,
   "useSkill":{"id":"my.medicine","instanceId":"use","level":1}}
]
```

| 字段 | 规则 |
| --- | --- |
| target | `ew4.item.` 加 def_item.xml 的非负十进制 id。必须存在，不能用物品名称；同名的不同 ID 要分别配置 |
| skills | 非消耗装备授予的技能实例数组，可主动/被动，必须引用本包 skills/traits 中已注册的扩展定义，不接受未定义原技能编号 |
| useSkill | 消耗品使用时执行的一个技能实例，不能同时给消耗品配置 skills，也不能给装备配置 useSkill |
| replaceLegacyEffect | 默认 false，保留并叠加旧 function/value 效果；true 关闭整个旧效果，由绑定技能替代 |
| value | 可选非负整数，覆盖原效果数值；在原版装备说明修正之后应用。替换旧效果时此值不参与扩展技能计算，扩展数值写在 effects/level/Lua 中 |
| targetCategory | 可选类别 ID，如 infantry/cavalry/artillery/naval/fort；all 取消类别限制。装备仅在将领指挥相应类别时授予技能；消耗品只允许用于相应类别 |
| requiresFlagSkill | 可选布尔值，覆盖装备需要旗手技能的限制；不改变物品的消耗品类型 |

实例沿用 `id/instanceId/level/order/x/y` 格式，见[规则接口](RULE_EXTENSIONS.md)。系统将本地 `instanceId` 前缀化为 `item.<物品数字ID>.<本地实例ID>`，完整长度须不超过64字符；因此不同物品可都写 ward，其状态和主动冷却分别保存。请为将领/兵种技能保留原来的 commander./unit. 前缀，避免与 item. 实例重名。未配置扩展时沿用原效果和旧存档内容指纹。

## 装备

装备仍通过指挥部库存和将领装备槽装卸，派遣或剧本初始任职时将授予技能合入部队技能列表。技能进入现有详情和主动技能栏；被动事件、战斗修正、移动上限、主动范围/召唤/传送及状态均遵循原技能 API 2。卸任时移除装备授予的技能；已施加到单位上的持续状态按原时限消退。参与本局的将领仍不能在指挥部中途换装。

装备主动技能保留自身费用、行动消耗和冷却。AI照常受被动效果影响，目前仍不会主动选择自定义主动技能。技能绑定只改变运行效果，不自动改写旧物品名称、描述或贴图，作者应配套维护原资源文字/图集。

## 消耗品

`useSkill` 必须是 `kind:active`、`target:self`、`endsAction:false`、`cooldown:0` 且三资源费用全0。消耗品沿用现有使用界面：选择己方部队，消耗一件库存，可在已行动或移动耗尽后使用。不额外暴露一个可免费重复使用的主动技能按钮；其执行事件为 `skill.activate`。

可组合治疗、资源、当前移动力和施加/解除状态；范围效果以所选己方部队为中心。需要选择敌方或格子的物品效果，可改为装备授予主动技能，或由范围效果实现。召唤/传送仍要求 cell 主动技能，不能塞进 useSkill。

保留旧效果时先执行旧效果，并沿用旧效果的满血/满级/士气资格检查，再执行 useSkill；替换旧效果时只执行 useSkill。失败不扣库存、不提交状态或随机数；成功仅扣一件并各推进一次账户/战局/会话修订。状态和 Lua 计数随战局保存，阵亡会走共用死亡处理。

## 外置 Lua

```lua
function medicine(ctx)
    local uses = (ctx.state.uses or 0) + 1
    return {value = 10 * ctx.level + uses, state = {uses = uses}}
end
```

效果仍通过 `script` 和 `function` 引用。`ctx.itemId` 为完整物品 ID，`ctx.skillId` 为技能定义 ID，`ctx.instanceId` 为完整实例 ID；非物品技能的 itemId 为 nil。其余 ctx、确定性随机数、状态字典、指令预算和可用操作与[技能 Lua 接口](RULE_EXTENSIONS.md#4-外置lua后端逻辑)一致。

外源代码使用包内离线 Lua；原 APK 的 SO/DEX 或外部 DLL 不能直接插入解释器。新增物品本体继续在作者完整 def_item.xml 中增加唯一 ID，并配套名称/描述/贴图、商店或初始库存，再绑定新 ID；本入口不创建库存、不自动把新物品上架。
