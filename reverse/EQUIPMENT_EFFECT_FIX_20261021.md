# Rewrite 装备实际效果修复与道具入口复核

后续：箱子入口及六种消耗品实际使用已接通，见 [消耗品交付记录](CONSUMABLE_ITEMS_20261021.md)。下文保留上一轮装备修复及入口核查时的状态。

日期：2026-10-21。范围：当前工作树源码。本轮按用户要求修复经验装备类型及32种装备数值，并复核剧本中箱子图标；没有新增消耗品使用流程。

## 箱子入口复核

用户指出正确：原版确有给部队使用道具的入口。`apk_work/original/assets/layout-568h.xml:798` 的 `btn_item` 使用 `button_items.png`；同文件第929行定义 `form_useitem` 使用物品窗口。图像已目视确认是箱子，并已导入 `rewrite/data/client/ui/image_recruit_hd3/button_items.png`。

Rewrite 的清单保留上述窗口和图像，但当前 `Ew4.Web/wwwroot/app.js` 的部队选择流程没有使用物品操作，`Ew4.Client/ClientGame.cs` 的动作分派也没有消耗品执行器。隔离浏览器实际选择己方部队后，工具栏只有“培训”“部队信息”。因此，原版有该功能，当前 Rewrite 仅保留资源，尚未接通使用、扣库存及士气/治疗/升级效果。本轮没有补入该流程。

## 已修复效果

- 黄金权杖、皇冠改为增加爵位经验；孙子兵法、战争论改为增加军衔经验，原有倍率保留。
- 32种名称对应的151条物品ID，实际攻击、防御、移动、野外补给或相邻支援数值按说明修正。同名重复ID一起修复；经验类型另涉及16条ID。
- 实际规则验证：弗格森步枪攻击加成由6改为8，固定基数10的伤害为18；普通帐篷野外补给由2改为4，50血回合结算恢复至54。

修正表在 `rewrite/Ew4.Legacy/LegacyEquipmentCorrections.cs`，由客户端显式启用，战斗目录与交易目录使用相同结果。仅物品表SHA256为 `ded737008eef2b8fefd1347d65d0e3fc9f051e82eb88f71bf2200247e9b80adc` 的原版内容启用；作者修改过物品表的Mod保留自身数值和效果。原XML、导入catalog和文本未改，通用Core算法未写入装备特例，Legacy读取器默认仍返回原始定义。

## 旧存档

修正会改变战局内容指纹。客户端首先正常校验新指纹，只有内容版本不匹配且启用原版修正时，才用未修正规则重新计算旧指纹并验证存档。校验成功后迁移已部署将领的装备加成及军衔/爵位经验字段，更新战局指纹并保存；再次读取不会重复叠加。迁移保留生命、行动状态、已耗移动、回合、随机状态、资金和领土，清除旧移动撤回快照。其他内容版本仍拒绝加载，没有跳过存档摘要或内容校验。

实现入口：`rewrite/Ew4.Client/ClientEquipment.cs`、`ClientGame.Load`、`ClientContent.WithoutEquipmentCorrections`。

## 验证

| 检查 | 结果 |
| --- | --- |
| 装备专项 | 450项，0错误；包含228条ID目录一致性、说明数值、真实伤害/治疗、经验类型、自定义Mod及三类旧档迁移 |
| 完整核心回归 | 310480项，0错误；装备专项计入commanderChecks=7832 |
| 客户端回归 | 1840项，0错误 |
| 浏览器 | 9项，0异常、0资源缺失；实际卸装/装备、派遣、保存、重载及存档菜单加载 |
| 编译 | Web构建、Android Compile均0警告0错误；Android显式指定本机SDK/JDK |
| 工具及补丁 | Node语法与git diff空白检查通过 |

专项源码：`rewrite/Ew4.Verify/EquipmentDescriptionChecks.cs`，入口 `--equipment-only`。浏览器工具：`tools/verify_equipment_descriptions.cjs`。核心结果：`rewrite/reports/core_validation.json`。浏览器结果及截图：`rewrite/reports/equipment-fix-20261021/`，其中 `browser-verification.json` 保存部队工具栏实际按钮清单。桌面及手机横屏截图已目视检查。

隔离预览：http://127.0.0.1:5214，PID41416。运行时在 `rewrite/reports/equipment-fix-20261021/artifacts`，账户及存档在同级 `web-saves`；玩家服务、玩家档案和其他任务服务未操作。没有生成新APK、发布或进行Android真机验收，已发行0.2.6不含本轮修复。
