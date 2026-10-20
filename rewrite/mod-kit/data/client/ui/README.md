# 已裁剪的界面图片

覆盖包路径示例：`client/ui/main.png`、`client/ui/commanders1_hd/Napoleon.png`。同尺寸替换无需改manifest；保留透明边距和画面中心，不要用白色填满原透明背景。

以上是路径示例，不代表每页都直接读取派生图。主菜单/选择页背景优先由布局引用的完整纹理生成；直接派生PNG覆盖的补充源码截至2026-10-20尚未完成最终回归和发布。应按发行版本实测，或修改原布局/纹理后重导入。

## 布局和交互必须配套

按钮图、控件位置/尺寸、点击区、层级、弹窗、返回和动画需一起核对。当前Menus和app.js仍有具体控件及事件绑定，导入布局树不等于自动执行全部交互，不能只换图而留下原版点击区。

`layout-568h.xml`转为`manifest.layouts`；`ConvertMenuAssets`、`MenuArt.prepare/apply`处理背景引用/裁切，`Menus.open/previewChapter`处理部分章节布局。其他大改页面按实际读取点接入或做客户端适配，不能在包内放JS/HTML/CSS期待执行。

逐页移植、账户/资源隔离、双端及原版→Mod A→Mod B→原版验收见[移植指南](../../../docs/porting/README.md)。完整UI配置、界面包依赖及自动功能绑定属于规划，不能按已发布接口分发。

| 文件/子目录 | 实际用途 | 检查位置 |
| --- | --- | --- |
| `main.png`、`title.png` | 主菜单底图、标题 | 启用后主菜单 |
| `campaign.png`、`campaign_background.png`、`conquest_background.png` | 战役/征服背景的不同裁切 | 对应选择页 |
| `commanders1_hd/`、`commanders2_hd/` | 将领头像，按manifest.atlases定位 | `Menus.portrait()`先找princesses_hd，再找两组commanders |
| `princesses_hd/`、`princesses_sketch_hd/` | 公主及未解锁素描 | 公主/指挥部 |
| `image_flag_hd/` | 选国及菜单国旗，如fra1.png | 国家列表；战场飘旗通常不是从这里读取 |
| `image_menu_hd/` | 菜单按钮、章节右栏底图、原标签 | 例如button_choosestage_france.png，其他五章各用不同图 |
| `image_ui_hd/`、`image_ui/` | 通用框、信息图标、技能图标等 | 技能ID0对应skill_01.png；先查manifest.atlases，不猜图集 |
| `image_item_hd/` | 装备/物品相关图像 | 指挥部商店、装备详情 |
| `image_recruit_hd/hd2/hd3`（实际目录为image_recruit_hd2等） | 招募卡片和编队标记 | manifest.profiles[id].card/cardMarker |
| `image_newgame_hd/` | 新局/教程等界面素材 | 由uiImage搜索顺序决定 |

修改前到`manifest.atlases.<图集>`查文件名，大小写及扩展名必须一致。原将领name常拼成`<name>.png`，但改版可有image属性；具体显示结果以生成的后端将领定义和manifest为准，不是只靠id推文件名。

源图集变更会由`LegacyModImporter.Convert()`重裁对应ui图。已转换覆盖包改PNG后重新pack/import/启用。战场动态图集不一定使用这些裁剪图；要同步主题请看[images](../images/README.md)。
