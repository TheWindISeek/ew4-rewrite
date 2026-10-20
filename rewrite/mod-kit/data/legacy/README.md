# 规则转换结果

`catalog.json` 保存兵种配置、国家继承、将领与技能引用、源文件SHA256及原生数值表。`runtime_rule_tables.json`、`metagame_rule_tables.json` 是已逆向并实现的战斗、账户和成长规则表。

只改将领 XML 后，不应手工随意补哈希；重新运行原资源导入器。若作者直接制作 rewrite 规则覆盖包，应提供完整且相互匹配的 catalog/原定义并运行 `pack`、`verify`。未知技能效果必须先实现后端规则，描述文字和图标不会自动产生效果。

基础文件建议只读；规则变化会影响存档兼容性，因此使用新的规则指纹目录。此目录没有支付、广告或原包可执行代码。

## catalog.json的具体结构

| 节点 | 内容 | 注意 |
| --- | --- | --- |
| `schema_version` | 当前2 | 不是Mod包schema，不能任意升号 |
| `source_sha256` | 原九份XML/INI文件哈希 | `LegacyRuntimeCatalog.VerifySources()`检查，重新转换生成 |
| `profiles[]` | id/category/grade/max_health/movement/minimum_attack/maximum_attack/minimum_range/maximum_range/food_consumption/traits/attack_bands/damage_multipliers等 | 后端实际兵种规则，不是画面profile |
| `variants` | 国家兵种继承表，键如fra，子键如militia.0 | 映射到profiles的稳定ID |
| `skills[]` | ew4.skill.0等、legacy_id、文字说明 | ID存在不等于可执行任意自定义效果 |
| `commanders[]` | id/legacy_attributes/skills | 原属性保存在legacy_attributes；四技能从原字段转换 |
| `source_trees` | 原XML的tag/attributes/text/children树 | 账户/装备等会使用，不能只改上层摘要 |
| `native_tables` | 从已逆向原库得到的数值表 | 不是从每个导入APK的SO动态恢复；改版SO新增规则不能保证覆盖 |

兵种例子ID为`ew4.unit.fra.militia.0`：国家变体fra、兵种Militia的slug、grade0。`minimum_attack/maximum_attack`是**单次基础攻击**，之后还会按血量/编队取攻击次数、加减属性和倍率，不是最终伤害区间。当前正常范围有下限钳位，但分布偏置、反向端点未完整拒绝；不要把错误端点作为平衡手段。

## 两张规则表

| 文件/字段 | 含义 | 加载入口 |
| --- | --- | --- |
| runtime_rule_tables.json / `unit_veterancy_rows` | 每级依次为[装甲,回合治疗,升级治疗,经验费用] | `LegacyCombatCatalog.Read()` |
| 同文件 / `training_money` | 各部队训练级别资金费 | 同上；要和级别行对应 |
| 同文件 / `commander_rank_xp`、`commander_nobility_xp` | 将领军衔/爵位晋级经验段 | 同上；不是每局直接获得经验 |
| 同文件 / `commander_rank_health_bonus` | 各军衔生命加成 | `LegacyRuntimeCatalog.FromJson()`及战斗目录 |
| 同文件 / `commander_nobility_healing` | 各爵位治疗加成 | 同上 |
| metagame_rule_tables.json / `title_commerce` | 各爵位商业能力底线 | `LegacyCommanderAccounts.Read()` |
| 同文件 / `rank_percent`、`title_percent` | 军衔/爵位继承或培养价格计算系数 | `CommanderInheritanceRules` |

两张表的schema_version当前1，source_sha256需要彼此与对应规则来源一致。扩展爵位/军衔不是只给XML填更大数字：相关经验、生命、治疗、商业、折扣、显示资源都要匹配。当前原生表范围外会报错，不能默默钳成已有等级。

## 推荐修改方式

普通兵种/将领/价格：修改完整原XML并重新导入。只调已知规则表：可在覆盖包放对应完整JSON，用pack/verify检查，再新局测试。原生代码技能、攻击时序判定、外交和辖区政策请看[源码索引](../../docs/SOURCE_INDEX.md)，不是在catalog里随意增加名字。
