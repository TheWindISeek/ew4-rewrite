# 后端原资源

`.btl` 保存某个剧本的国家、单位、设施、事件和地图范围；`def_map.xml` 登记地图，相关 `.bin` 为地形网格。国家、将领、兵种、商店、设施等 XML 及 `stringtable_cn.ini` 为配置。辖区优先读取该BTL的JURI v1扩展，没有扩展才按开局中心自动计算，不写死在公共底图。逐格制作及协议见[手工辖区指南](../../docs/porting/BTL_JURISDICTIONS.md)。

修改这些文件后，用原包资源导入工作流重新转换，使 `legacy/catalog.json` 的源哈希和规则同步。添加地图必须注册地图 ID 并提供对应地形与显示资源，BTL 的地图引用、范围及单位坐标需合法。城市/工厂/马厩是辖区中心，港口独立；本版机制不由贴图决定。

不要直接往这里添加原包可执行代码，也不要把存档放在这个目录。

## 文件及字段定位

本目录不是完整APK的assets。原图集XML、动作定义、PVR等转换输入需从作者自己的原包工作副本取得。

| 文件 | 查找节点与字段 | 用途/配套 |
| --- | --- | --- |
| `def_commander.xml` | `<commander id="1" name="Napoleon" ...>`；`country/rank/nobilityrank/star`、`infantry/cavalry/artillery/warship/fort`、`business/movement/training`、`skill1...skill4`、`item1/item2/price` | 将领定义；保留ID。name是资源键，改名要同步头像/文字。已持有的将领实例不保证随定义重置 |
| `def_army.xml` | `<country name="fra" ref="...">`下`<army name="Militia" grade="0" ...>`；`strength/movement/minatk/maxatk/weapon/minatkrange/maxatkrange/consumption` | 兵种生命/行动/基础攻击/武器/射程/粮耗；grade是独立编队档 |
| `def_card.xml` | `<card id="0" type="recruit" army="Militia" grade="0" price="60" industry="0" ...>` | 招募、建造、运输卡；price为资金，industry为工业，不在def_army里改招募价 |
| `def_construction.xml` | 建筑type下各等级，保留原属性名 | 城市、工厂、马厩、港口、农田定义；由`LegacyBuildingCatalog.Read()`解释 |
| `def_capital.xml` | 首都id记录 | 首都元数据，配套`manifest.capitals`和原图集 |
| `def_installation.xml`、`def_terraintype.xml` | 原类别和属性 | 设施/地形战斗防御，由`LegacyCombatCatalog.Read()`加载 |
| `def_mapterrain.xml` | 分类下`idx/image` | 地形索引/密度/贴图，图案不等于可通行性 |
| `def_item.xml` | 物品ID、效果引用、交易定义 | 装备/商店/使用效果；改文字不产生新效果 |
| `def_princess.xml` | `Princess`节点 | 公主及关联定义，配套头像/素描 |
| `def_warzonetech.xml` | 章节/科技节点 | 战区科技，`LegacyWarzoneTechnology.Read()`加载 |
| `def_dialogues.xml` | 对白记录及文字引用 | 还需剧本事件触发；不是所有文字都由BTL直接保存 |
| `def_battleline.xml` | `<line name="imperialeagle" start="1793" end="1820" countries="fra" ...>` | 大章列表；**顺序决定章节号**，新增章放末尾 |
| `def_battlelist.xml` | battle的`name/file/map/age/commander/centerx/centery`，可选`file2/hide/countries` | 关卡介绍/战略标记/备选剧本；hide与实际解锁不是同一层 |
| `def_conquestlist.xml` | 原国家/征服定义 | 规则源之一；画面分页另在frontend配置 |
| `def_areaname.xml` | `areaname`的`id/name` | BTL地点ID到原名；中文另用city-names.json |
| `stringtable_cn.ini` | `name_Napoleon`、`name_Militia/desc_Militia`、`name_skill_01/desc_skill_01`、`text_conquest_age_7` | UTF-8的`键=值`，重复键末项生效。关卡用`title_<battle name>`和`desc_<battle name>` |

## 四技能编号

`skill1`～`skill4`使用0～39，`-1`为空；技能0对应`name_skill_01/desc_skill_01`，图标`skill_01.png`。编号40及以上会拒绝导入。技能效果在规则代码中实现，改名称、图标和说明不会自动生成技能效果。

移植新的原生技能时，将不支持编号从XML槽清为-1，在包根`rewrite/extension.json`注册自己的JSON/Lua定义并用commanderBindings绑定；无需将技能塞回旧格式字段。数量/坐标可变，操作步骤和限制见[API 1指南](../../docs/porting/RULE_EXTENSIONS.md)。

## 一个可操作的属性修改

在原资源副本的完整`def_commander.xml`里找到`id="1"`，只把`business="3"`改成`business="4"`。将该完整XML压入ZIP根目录，或放`assets/def_commander.xml`后压ZIP，在游戏Mod入口导入。文件选择器导入的是包，不能直接选单个XML。

转换器会重建catalog及源哈希，作者不用手补`source_sha256`。启用后在新逻辑档案核对定义及新招募结果；已有账户实例、基础招募价、军衔爵位折扣是不同层。

## BTL与地图

`campaign1_01.btl/conquest1.btl/tutorials1.btl`都是二进制剧本，不是JSON。请用兼容BTL版本2的编辑器维护，不按猜测偏移写入。`def_map.xml`的实际地图记录：

```xml
<map id="1" name="europe" continent="europe" file="europe.bin" w="4992" h="3582" tile="map1.bin" textpos="maptextpos_europe.xml" tilesize="64" pattern="map_pt.png" patternsize="512"/>
```

`file`为地形网格，`tile`为画面块索引，两者格式不同。配套依赖见[地图说明](../client/maps/README.md)。辖区使用BTL的JURI及`TerritorialJurisdictions.FromAssignments()`，无JURI才由`Create()`自动建立；后续存在战局存档，不能靠给PNG涂色或修改公共底图固定分区。旧编辑器不保证保留扩展尾部，需先strip、编辑主体，再重新导出/写回。

`LegacyCatalogConverter.Sources`列出九份源哈希关联文件；`Convert()`重建规则，`ClientContent`构造函数读取章节/地图/XML，`Battle()`读取BTL，`Start()`创建新局。[源码索引](../../docs/SOURCE_INDEX.md)列出文件位置。新增BTL优先原格式导入，直接pack默认不会自动扩充scenarios，见[分步示例](../../examples/README.md)。
