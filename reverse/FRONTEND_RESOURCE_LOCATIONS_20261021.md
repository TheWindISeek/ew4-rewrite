# 缺失显示资源定位

后续实现：[单项操作的原版费用面板](SINGLE_ACTION_COSTS_20261021.md)。本页关于培训/升级/运输报价及单项卡片弹窗的未修复记录已由该实现覆盖；其他资源定位结论保留。

日期：2026-10-21。承接 [手机四图证据与两图 Mod 参考](references/frontend_user_20261021_mobile/README.md)及 [FB-01～10](../rewrite/GOALS.md)。本轮仅资源/布局/调用证据调查，未改游戏代码、运行服务、存档或 APK，未操作设备。

## 结论与材料

已定点核对 **24 个原版图集裁片、7 个布局**：24 个名称均能在当前 rewrite manifest 中找到，7 个布局也均已导入；这只证明资源可用，不代表实际界面已执行原布局或使用了正确数据。主要缺口是表现流程、数据绑定和报价/文案传递，不能统一归因为缺 PNG。

- [原版资源预览](reports/frontend-resource-location-20261021/resource-contact-sheet.png)：直接从原版 HD 图集裁切生成，非重绘素材。
- [机器可读索引](reports/frontend-resource-location-20261021/resource-index.json)：原图集/定义路径、矩形/锚点、当前同名图集映射、派生图存在性、布局 XML 和源文件 SHA256。
- [复核工具](../tools/reverse/locate_frontend_resources.ps1)：运行 `powershell -File tools/reverse/locate_frontend_resources.ps1` 或在当前 PowerShell 中直接执行该脚本。仅生成定位报告/预览，不调用游戏。

原版图集 XML 使用 `Texture` 和 `Images` 并列根节点，工具按片段包装后用 XML API 解析；不把它当作普通单根 XML。坐标随图集分别记录，不混用 HD、普通图集和其他同名图。

## 1. 战役存档国旗：FB-01

**原版布局**：[layout-568h.xml](../apk_work/original/assets/layout-568h.xml) 的 `form_save`（第 838 行起）。`group_save_0` 为自动档，`group_save_1～6` 为手动档，均有 `image_flag`：

| 控件/素材 | 定位 |
| --- | --- |
| 自动档国旗 | `form_save/group_save_0/image_flag`，y=53、水平居中。 |
| 手动档国旗 | `form_save/group_save_1～6/image_flag`，y=47、水平居中。 |
| 菜单国家旗 | `image_flag_hd.xml/png`，例 `fra1.png`=(105,1,51,30)，`usa1.png`=(261,32,51,30)。rewrite 已有 `data/client/ui/image_flag_hd/` 对应 PNG。 |
| 存档确认角标 | `image_ui_hd` 的 `button_ok_gray_noshadow.png`、`button_ok_noshadow.png`。 |
| 自动保存背景 | `image_ui_hd/save_grayboard.png`；`pattern_stage_intro.png` 为装饰，不是国旗。 |

同名国旗还在 `image_ui_hd`、战场 `buildings` 中存在。菜单国旗与战场四帧旗尺寸/锚点不同，后续不能只按名字任选图集。

**原生证据**：`00042db0.c` 创建 `form_save` 并依次调用 `00042c60.c` 填七槽；后者读取存档摘要再调用 `0x42aa8`。`0x42aa8` 尚无本地 C 导出，资源交叉引用可确认其使用确认角标，不能据此虚构完整国旗绑定过程。

**rewrite 缺口已见源码证据**：`menus.js:84～86` 开战役传入 `country=null`；`ClientGame.Start` 直接保存 `command.Country`，`ClientSlot.Country` 及 `ClientSaves.Saves` 继续原样保存/返回；`menus.js:189` 仅在 `slot.country` 非空时创建国旗。因此该路径具备“战役正常开局但存档列表无旗”的直接解释。后续应根据会话真实玩家势力取得显示国家/变体，并考虑旧档回退；本轮未修复或读取用户存档验证。

## 2. 胜利弹窗、分档回合与奖励：FB-08

**原版布局**：`form_victory`（第 679 行起，346×210）。这才是参考图 5 的完整结算面板对应结构。

| 信息 | 控件/资源 |
| --- | --- |
| 战役名称、完成星级 | `text_battle`、`lbox_stars`；`star_middle.png` 为亮星，`diffcult_1.png` 为暗星。 |
| 实际回合 | `text_round`、`text_roundval`。 |
| 分档回合门槛 | `group_starlevel/image_star` 使用 `image_ui_hd/star_board.png`；`text_star`、`text_star2` 是动态门槛数字。 |
| 奖励与获得 | `text_award`/`text_award_val`、`text_gain`/`text_gain_val`，原版图标为 `image_ui_hd/medals.png`。 |
| 将领栏及退出 | `lbox_general`、`btn_continue`、`btn_restart`、`btn_exit`；按钮为 `btn_common_green.png`、`btn_common_blue.png`。 |

**原生数据绑定已确认**：[0004be04.c](native/decompiled/0004be04.c) 读取 `GameRound` 写 `text_roundval`，读取 `AwardMedal`、`CollectMedal` 写两个奖励值；战役对象 `+0x50/+0x54` 写 `text_star/text_star2`，完成星级来自 `FUN_0017eeb8`。胜/负分支切标题、按钮、星级区域；该函数还填将领栏。原版是勋章字段，参考 Mod 的图标替换/奖励类型及 13/10、150/40 不能直接推广为原版规则。

**另有两种相关界面，不能混淆**：

- `form_roundturn`（第 652 行起）包含 `text_roundnum`、`text_roundmin/max`、`star_board.png`、收入/粮耗和将领栏；`0x421f4` 打开该表，`000422e8.c` 中可见回合与门槛绑定，但其反编译有缺失函数上下文，模式条件需继续补证。
- `form_victorytext`（第 1105 行起）只是胜利文字演出：`board_victory.png` + `tex_victory.png`；原版也有 `tex_victory@2x.png`。它不含上述完整结算栏。当前 rewrite 有 `images/tex_victory.png`，不能因已有这张图就认定结算已完成。

**rewrite 状态**：相关布局和关键裁片已在 manifest；当前 `app.js:342` 仅将结束状态写为“胜利/本局结束”，在 `app.js/menus.js` 未找到 `form_victory` 的实际接入。需后续连接规则结果、评分、奖励及将领投影，而非新画一个弹窗或把布局中的示例数字作为真实数据。

## 3. 地图一对一操作的费用小面板：FB-06/10

**准确定位**：`form_game/group_funcres`（第 822 行起，84×56），不是左上资源条，也不是招募卡片。

| 元素 | 定义 |
| --- | --- |
| 面板背景 | `image_ui_hd/board_button.png`，原图集矩形 (707,521,168,112)，锚点 (16,6)。下方自带指向单位的尖角。 |
| 两行费用 | `image_money`=`marker_money.png`；`image_apply` 初始=`marker_industry.png`；动态文字 `text_needmoney`、`text_needapply`，字体 `font_text_4`、各 50×19。第二行实际语义应追随操作，不把初始图标视为所有操作固定资源。 |
| 直接确认 | `btn_done`，使用 `image_recruit_hd3/button_ok.png`，布局为 (33,55,33,33)。 |
| 操作按钮 | 培训 `image_recruit_hd3/button_training.png`；升级和运输分别在 **`image_recruit_hd`** 的 `button_buildupgrade.png`、`button_buyship.png`。不能假定全部来自 hd3。 |

`00090808.c` 取得并初始隐藏 `group_funcres`，确认它是原版战场控制器管理的独立组。其打开/报价/禁用调用条件仍需继续追踪，不能只凭静态资源认定任意操作均适用。参考图 6 的红色费用也应由可支付性决定，不能烘焙到贴图。

`board_resources.png` 实际用于 `form_game/image_resback` 左上资源条，**不是**这里的报价框。招募卡片另用 `resource_small_money.png`、`resource_small_industry.png`；`0003fe8c.c` 可见招募报价及不足变红逻辑，同样不应与 `group_funcres` 混用。

**rewrite 两处直接线索**：

- `confirmUnitAction()`（`app.js:497`）统一弹自定义 250×150 信息窗，插入 `unitCard(selected.profile)`；未见 `group_funcres`、`text_needmoney/needapply` 的表现接入。
- 该调用没有传 `cost`，而 `unitCard()`（`app.js:448～454`）以 `cost?.[kind] ?? 0` 显示数字。这解释了确认卡片的两个 0 是前端默认值，不能当作真实免费报价。后端 `WorldImprovements.cs:91` 的 `training.resources` 错误仍按实际费用判断。仍需后续复现确认整条链，不能由此取消后端资源校验。

## 4. 培训入口和提示文字：FB-05/09/10

培训按钮资源在 `form_game/group_func/btn_training`（第 790 行），字体族配置见 `font_resource.xml`，中文词条见 `stringtable_cn.ini`。资源和布局只说明“有这个按钮”，不说明何时应显示；培训资格仍应结合原动作条件核对，见 [培训与升级原规则](TRAINING_AND_UPGRADES.md)。当前前端仅按 `own && target.commanderId` 创建培训入口，缺少其他资格投影的可见性检查。

**“已保存”拆行与英文资源错误**：当前提示来自 rewrite 自有 `notice()` 和 `style.css:20` 的 `#notice`；样式含 `overflow-wrap:anywhere`。`failure()` 未映射 `training.resources`，会回落到核心的英文错误。此次在原版布局和中文词条中未定位到这两段提示的完全对应专用资源，不能宣称找到一张替换图片就能修复。

原版存在 `form_floattext`，背景 `box_skilldetail.png`、箭头 `box_skilldetail_arrow.png`，文字是 `font_text_1` 的 110×135 区域；另有 `form_messagebox` 的通用确认/取消框。这些已定位，但**没有证据证明它们就是截图中的保存成功/培训错误提示**，暂不作为其精确原版归属。词条 `title_unitinfo` 是“信息”标题，不是培训错误正文。

## 5. 士气升降表现：FB-07

已定位到 `buildings_hd.xml/png` 的四张原版素材，原锚点不可丢失：

| 素材 | 原图集 x,y,w,h | refx,refy | 图像观察 |
| --- | --- | --- | --- |
| `morale_down1.png` | 896,276,85,105 | 44,31 | 向下箭头/鹰形标识。 |
| `morale_down2.png` | 883,163,88,111 | 45,32 | 双向下箭头/鹰形标识。 |
| `morale_down3.png` | 747,588,85,60 | 42,32 | 云与问号，不能仅凭文件名推断为“下降三级”。 |
| `morale_up.png` | 190,829,82,108 | 41,36 | 金色鹰形标识与向上箭头。 |

`00065b14.c:88～104` 创建并保存四张 sprite。当前 manifest 的 `buildings` 也有这些名称，普通图集与 HD 的尺寸/锚点不同；本报告为原版 HD 取样，实际接入时需匹配资源密度。`battlefield.js/effects.js` 中未找到对应名称的使用。**目前只定位到素材和原生初始化，不宣称已还原触发条件、持续时间、运动、淡出或其与夹击规则的对应关系**。

## 继续定位的边界

后续优先补 `group_funcres` 报价与显示条件、存档槽旗帜赋值原函数 `0x42aa8`、士气演出调度；可按函数地址定点导出，避免重扫/重导全库。保存通知的精确原版归属仍未找到。建筑不能升级、胜利未触发和撤回异常还包含规则/状态问题，不属于“补齐前端贴图”即可解决的范围。
