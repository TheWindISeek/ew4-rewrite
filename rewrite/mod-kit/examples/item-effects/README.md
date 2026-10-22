# 物品/装备 JSON 与 Lua 示例

本目录是只包含规则文件的覆盖包，可复制为作者目录后打包。字段完整说明见[物品接口](../../docs/porting/ITEM_EFFECTS.md)。

- ew4.item.8：关闭帐篷原治疗，改授予减伤/回合治疗和主动补给。
- ew4.item.9：保留原治疗并将数值改为17，另授予2级护佑；与物品8使用相同本地实例名，但状态独立。
- ew4.item.13：替换原医疗包，外置 Lua 按等级与已使用次数治疗，并施加持续2个所属方回合的振奋状态。

示例不赠送物品或改写原物品文案。获得对应物品后正常装备/使用即可验证；当前游戏已有库存属于各 Mod 的独立档案，不会从原版自动带入。

```powershell
.\Ew4.ModTool.exe pack "D:\mods\item-effects" --id=item-effects --name="物品效果示例" --version=1.0
.\Ew4.ModTool.exe import "D:\mods\item-effects"
.\Ew4.ModTool.exe list
.\Ew4.ModTool.exe export <key> "D:\mods\item-effects.ew4mod"
```

需要包含本接口的新引擎。将 .ew4mod 从主菜单 Mod 入口导入并启用；修改 JSON/Lua 后重新打包形成独立逻辑版本。
