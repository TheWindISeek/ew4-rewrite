# 通用技能 API 2 示例

复制本目录的 `rewrite/` 到自己的作者覆盖包，再使用支持 API 2 的新版工具和游戏打包、导入、启用，开法国 conquest1 新局。法国一级重炮附带六个测试技能；本示例不修改正式胜负条件。

```powershell
.\Ew4.ModTool.exe pack "D:\mods\common-skills" --id=common-skills --name="通用技能示例" --version=1.0
.\Ew4.ModTool.exe verify "D:\mods\common-skills"
.\Ew4.ModTool.exe import "D:\mods\common-skills"
```

| 技能 | 验证内容 |
| --- | --- |
| 召集步兵 | 地块目标、兵种模板、容量及海陆限制、新部队不可立即行动 |
| 战术转移 | 地块目标、非路径移动、友方/无主落点、消耗行动 |
| 范围炮击 | 半径一圈、排除友军、Lua按技能等级计算、一次费用和冷却 |
| 群体急行军 | 范围增益、移动上限、恢复剩余移动、伤害增减 |
| 毒弹 | 敌方状态、最多三层、回合触发、持久计数、死亡结算 |
| 解毒 | 移除状态及其脚本计数 |

绑定兵种可以换成自己的 catalog profiles ID。将相同技能实例放入 `commanderBindings` 即可绑定将领，实例ID改用 `commander.` 前缀，避免与兵种重复。技能脚本无需编译，接口说明见[Mod修改指南](../../docs/porting/README.md)和[完整API字段](../../docs/porting/RULE_EXTENSIONS.md)。旧发行版不支持 API 2，须使用包含此后端更新的版本。
