# 完整图集：坐标、锚点与HD

此处PNG可能同时用于战场sprite、动画叶节点和字库。覆盖路径为`client/images/<原名>.png`。

`manifest.atlases.commanders1_hd["Napoleon.png"]`实际字段为`x/y/w/h/refx/refy`；原基础坐标x1560、y1092、宽高156，refx1、refy0。换整张图保持坐标/布局时可只覆盖PNG；重新排版需同步完整manifest，并更新对应ui裁剪图。不要把156×156单头像替换到整张图集文件名下。

`app.js`的`sprite()`读取manifest.atlases；`animation()`读取动画JSON自带atlas，它不保证使用manifest同一份坐标表。原资源导入从图集XML生成这两类索引，避免手工漏同步。

| 图像例子 | 关联内容 |
| --- | --- |
| `commanders1_hd.png/commanders2_hd.png` | 将领整图集，与ui同主题 |
| `buildings_hd.png/buildings.png` | 城市、港口、运输船、旗杆及战场旗帧等；以atlas实际键为准 |
| `image_menu_hd.png` | 菜单图集与裁剪条目 |
| `eff.png` | 粒子小图；不因为名字无hd就一定存在另一个高清版本 |
| `army_*.png` | 兵种动画贴图，配套animations库的atlas |
| `font_*.png` | 位图字库，配套manifest.fonts度量 |

战斗数字使用 `font_fight_num_hd.png`，基础度量在 `client/fight-font.json`；APK重新导入时可用配套FNT覆盖到 `manifest.fonts.font_fight_num_hd`。普通界面文字仍使用内置WOFF2字体。

战场旗帜由`battlefield.js`的`flagFrame()`每100ms轮换1～4帧，`flagPosition()`使用当前位置偏移(-27,-45)。只替换选国国旗不会改这些战场帧。改旗帜图形请保持各帧锚点一致。

PNG解码预算：最长边8192、最多16Mi像素。图集允许有未引用旧条目，但UI裁剪坐标必须合法；原改版里无效/重复条目可能需要导入兼容处理，见真实APK测试报告和[排错](../../../docs/TROUBLESHOOTING.md)。
