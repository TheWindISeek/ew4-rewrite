# 周期补给与双边外交示例

独立示例，适配内置第一征服。推荐选择法国：开局选择出征后，第 3、6、9……回合扣法国金币/粮食/工业零件，并给作为演示军团的 AI 势力补给。资源不足时整笔撤回、累计短缺次数并停止出征。第 4 回合军议可将法国与该势力改为敌对、中立或恢复阵营关系，不改变其他国家。

示例固定使用 `conquest1.faction.0.fra`（法国）和 `conquest1.faction.2.koi`（演示军团）。它们只是原素材中的两个国家，本例不新增国家或改写其名称/地图；改编其他剧本时须替换所有对应国家 ID、条件与资源数量。开局国家选择不是国家运行 ID，具体见 API 的 `factions[].id`。

在自己的独立作者目录放入本例的 `rewrite/`，用包含本次接口增补的 ModTool 执行：

```powershell
.\Ew4.ModTool.exe pack "D:\mods\recurring-diplomacy" --id=recurring-diplomacy --name="周期与外交示例" --version=1.0
.\Ew4.ModTool.exe import "D:\mods\recurring-diplomacy"
.\Ew4.ModTool.exe export <返回的key> "D:\mods\recurring-diplomacy.ew4mod"
```

默认事件仍为一次性；本例只有军费事件设置了 `repeat`。期间没有出征就跳过，不补扣；短缺结果可以改成士气下降、转为敌对或增加变量后触发其他弹窗。重复弹窗使用同一个 `repeat` 字段即可，按钮数量和条件不变。

此目录为可选 Mod 源码示例，不会自动加载进已有剧本。旧 0.2.4 安装包尚不含新接口；完整字段与存档语义见[接口说明](../../docs/porting/SCENARIO_EVENTS.md)。
