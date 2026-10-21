# 胜利结算分工交接

日期：2026-10-21。工作目录：`E:\Workplace\game\o4mod`。

用户指定分工：**当前会话负责渲染及跳过回合加速，另一个新会话负责补入胜利结算。** 本文件供新会话直接读取，无需重读完整聊天或工作日志。初稿只建立交接；用户随后已安排另一会话并授权性能实现，最新进展与已验证接口见末尾协调记录。

## 分工与范围

| 工作 | 负责会话 | 范围 |
| --- | --- | --- |
| RW-01 / RW-03 | 当前性能会话 | 手机卡顿/发热、按需重绘、可见对象、缓存/像素/特效预算、跳过演出、有界 AI 批处理、桥接/状态/HUD 更新合并，详见 [联合优化规划](ANDROID_RENDER_PLAN.md)。 |
| FB-08 胜利结算 | 新结算会话 | 恢复原版完整结算面板，接入真实回合、星级及分档门槛、奖励/获得、将领栏、继续/退出，以及失败分支。核对数据来源、重复结算和存读档恢复。 |
| FB-02 胜利未触发 | 新结算会话关联核查 | 复核“占完红点仍不能获胜”与实际条件/版本；区分结果未生成、结果未投影和界面未打开。若属于胜负判定缺陷，依据原规则与可配置条件修复，不把所有关卡统一成占完红点。 |
| 其余 FB 项 | 本交接不分配 | 存档国旗、撤回、升级、培训、单一操作费用弹窗、士气和提示文字仍有记录；本次分工不自动要求结算会话一并修复。 |

## 先读这些材料

1. [原版资源定位报告](../reverse/FRONTEND_RESOURCE_LOCATIONS_20261021.md)，重点第 2 节：完整胜利面板、回合信息和数据绑定；其他节按需查阅。
2. [目标/反馈清单](GOALS.md) 的 FB-02、FB-08，以及 [原版目标圈与解锁记录](../reverse/OBJECTIVES_TERRITORY_20261019.md)。开发验证不等于用户验收，旧记录不能关闭新反馈。
3. [六图归档说明](../reverse/references/frontend_user_20261021_mobile/README.md)。**前四张是真机问题证据，后两张是原版 Mod 参考**；图 5 为结算目标参考，非当前 rewrite 已实现的证据。

原图 5：[胜利结算参考](../reverse/references/frontend_user_20261021_mobile/7e440c507585552c5fae6e53f8f54d3f.jpg)。其中 13/10 回合、150/40 奖励仅属于该 Mod 示例，不写死为所有剧本规则。图 6 为单一操作费用面板，与胜利结算不是同一功能。

有需要再看 [资源索引](../reverse/reports/frontend-resource-location-20261021/resource-index.json)、[素材预览](../reverse/reports/frontend-resource-location-20261021/resource-contact-sheet.png)。索引超过 32KiB，按结构只取相关布局/素材，不回传全文。

## 已确认的原版入口

原始材料在 `apk_work/original/assets/`，不改原件；当前可运行资源在 `rewrite/data/client/`。

| 内容 | 已定位证据 |
| --- | --- |
| 完整胜利/失败面板 | `layout-568h.xml` 的 `form_victory`，346×210，第 679 行起；已在当前 manifest 中。 |
| 战役名/完成星级 | `text_battle`、`lbox_stars`；亮星 `image_ui_hd/star_middle.png`、暗星 `diffcult_1.png`。 |
| 实际回合及门槛 | `text_roundval`、`group_starlevel`、`text_star/text_star2`，背景 `image_ui_hd/star_board.png`。布局示例数字不能作为实际数据。 |
| 奖励与获得 | `text_award_val`、`text_gain_val`；原版图标 `medals.png`，原生绑定 `AwardMedal`、`CollectMedal`。参考 Mod 的资源图标/奖励语义可能不同。 |
| 将领/操作按钮 | `lbox_general`，`btn_continue`、`btn_restart`、`btn_exit`，原绿/蓝按钮图；按胜负分支显示。 |
| 原生赋值 | [0004be04.c](../reverse/native/decompiled/0004be04.c)：读 `GameRound`、`AwardMedal`、`CollectMedal`，战役对象 `+0x50/+0x54` 填分档门槛；`FUN_0017eeb8` 返回星级，需继续核对算法和元数据导入。反编译为推断类型，不当作原源码。 |
| 回合信息面板 | `form_roundturn`（第 652 行起），另有 `text_roundnum`、`text_roundmin/max` 与收入/粮耗；本项按结算关联需求处理，不扩大为全部前端重做。 |
| 胜利文字演出 | `form_victorytext` + `board_victory.png` + `tex_victory.png`，只是一段演出，不是完整结算面板。 |

这组素材及布局已导入，重点在数据和交互接通，暂不需要新画素材或更换整体 UI。

## 当前源码事实

- `Ew4.Web/wwwroot/app.js` 的 `refreshHud()` 目前只在结束时写“胜利/本局结束”，资源定位时未找到完整 `form_victory` 接入。函数名比行号稳定，开始时重读当前版本。
- `Ew4.Client/ClientGame.cs` 的 `View()` 输出 `phase/outcome/round` 等状态，`InstallSession()`、`Load()` 会调用 `CampaignProgress.Settle()`。
- [CampaignProgress.cs](Ew4.Client/CampaignProgress.cs) 当前负责黄圈完成后隐藏关的持久解锁；函数名叫 `Settle` **不等于已实现星级、勋章和完整战后结算**。新功能需保留既有解锁且避免重复发奖。
- `WorldVictory.Evaluate`、`LegacyScenarioHost` 及规则扩展已有胜负条件支持。先对照当前实现和原元数据，再决定缺哪些字段；不为打开面板伪造 Victory，不修改随机序列或放宽通关条件。
- 渲染与状态安装正是性能会话将要优化的部分。跳过不能只解除等待；结算会话也不能依赖每个中间 AI 动作都安装完整界面才能获知最终结果。

## 文件分工及共享边界

| 文件/区域 | 编辑约定 |
| --- | --- |
| `app.js` 的 `render/runAi/installState`、跳过按钮和表现等待 | 性能会话主负责。结算会话只加最小初始化/结果同步入口，不重写循环、动画调度或 AI 请求路径。 |
| 新增结算前端模块/样式 | 结算会话主负责；建议独立 `victory-ui.js`、`victory-ui.css`，需要的 HTML/加载项最小接入，不复制整份 app.js。名称为分工建议，不是既有 API。 |
| `ClientGame.cs` 的 `Action/InstallSession/View` | 共享。性能会话处理批次执行/同步；结算会话处理结算数据与持久化语义，建议放独立 partial/helper 后仅加调用/投影。改前重读当前 diff，不整体替换文件。 |
| `CampaignProgress`、结算模型/评分/奖励、相关专项验证 | 结算会话主负责。优先新增独立模块，保留既有黄圈解锁；仅在真实需要时扩展账号/存档字段并兼容旧档。 |
| `battlefield.js`、`effects.js`、Android 生命周期及渲染缓存 | 性能会话主负责。结算会话通过结果层展示，不把评分/奖励逻辑塞进绘制循环。 |
| 原版资源、现有规则扩展、Mod 数据 | 保留来源与已有改动。引用已有素材；新字段应兼容原版和 Mod，避免无关格式/目录重写。 |

共享文件不可避免会有少量交集。实施前和修改前都查看 `git status --short`、相关 `git diff`，按函数做窄补丁；不回退、覆盖或暂存隐藏其他会话的修改。若需要重构对方负责的核心循环，先在本文件“协调记录”写明所需接口/原因与未完成部分，避免双方独立重写同一区域。

截至写交接时，工作树已有其他来源的运行代码修改，包括 `ClientContent.cs`、`ClientGame.cs`、`ScenarioRules.cs`、`WorldState.cs`、`ExtensionLoader.cs`、`LegacyScenarioHost.cs`、`app.js`、`battlefield.js` 及新增 `ScenarioFactions.cs`；可能继续变化。它们不是本交接新写的代码，不得按旧报告回退。民国相关未跟踪目录也不在本任务范围。

## 双方必须保留的集成约定

1. 结算判断来自后端已提交状态；前端渲染帧数、动画播放或跳过速度不能决定获胜、星级或奖励。
2. 可新增可选 `battleResult` 等结果投影，具体字段由结算会话依据真实数据设计并在下方记录；当前没有约定该字段已经存在。普通与跳过路径必须返回相同结果语义，新增 AI 批次不能丢字段。
3. 表现层提供独立、可重复调用的结果同步入口。相同局/同一结算结果重复刷新不得反复弹窗或发奖；新局、重开、切 Mod/账号需要正确重置。不能仅用会变化的 revision 或只用关卡 ID 当结算唯一标识。
4. 性能会话可省略中间演出/HUD 刷新，但到 Finished、需要玩家选择的事件、己方可操作回合或错误边界，必须提交并同步最新状态；最终结算入口不被“跳过渲染”一并绕过。
5. 奖励提交与存档恢复应可核对且防重复；打开/关闭面板、继续/退出、重复刷新和读档不再发一次奖励。不擅自把每步恢复能力改成仅整轮末尾保存。
6. 若新增胜利文字演出，跳过模式可省略其等待，但仍展示可操作的完整结果面板。隐藏关解锁、对白/事件顺序及失败分支保持正确。

## 结算会话的实施与验证

1. 定点核对原版星级、门槛、奖励和关卡元数据，以及当前账号/存档字段。先记录缺口；不补假值或直接套参考 Mod 数字。
2. 实现规则/数据投影和持久结算，再接独立面板、将领栏及按钮；记录实际新增字段、前端入口和改动的共享函数。
3. 用隔离账户和固定测试场景验证：普通/跳过到达相同结果，实际回合和门槛取值正确，奖励只提交一次，失败不误发胜利奖励，黄圈解锁保留，存读档/重开/继续/退出均正确。评分边界需覆盖门槛前/等于/超过，不能只测一种星级。
4. 桌面及手机横竖屏检查文字、奖励、将领槽和按钮，无溢出/遮挡/缺图；使用原资源与当前离线字库。根据涉及范围运行相关核心/客户端与浏览器检查，保留证据。
5. 新开独立测试服务并检查端口占用，使用独立输出目录/用户数据；不要重启玩家服务、使用玩家账户跑自动化、覆盖当前存档，或与另一会话共享构建输出并行覆盖。旧文档中的端口/PID仅是历史记录，现场核对。
6. 本次交接不要求构建/发布新 APK，不触碰正式发行附件；Web/Android 需要的编译验证按实际改动进行。未真机运行必须写明，不能把浏览器模拟手机当作 APK 验收。

完成一个阶段后先保存成果与验证说明，再补短锚点和简短日志。结算工作建议使用独立 `reverse/VICTORY_SETTLEMENT_20261021.md` 记录详细证据；共享 STATUS/WORK_LOG 只追加简短入口，避免两边反复改写大段内容。

## 协调记录

2026-10-21 后续：胜利Mod扩展完成，详见[新记录](../reverse/VICTORY_MODDING_20261021.md)。`battleResult`新增可空presentation；事件多次替换规则沿用另一会话已有接口。胜败界面可内嵌布局/绑定，默认原版；六将领槽、白色门槛不越框及衬线数字按用户反馈修正。最终同步入口未改；专项32+459、浏览器39及Web/Android Compile通过，独立5213 PID35072。

| 项目 | 当前状态 |
| --- | --- |
| 性能会话 | 第一轮完成：`ai_batch`、地块增量、按需/遮挡/后台绘制、可见对象准备和像素预算。客户端1786/联合浏览器14/渲染回归21项及Web/Android Compile通过；详见 [实施记录](../reverse/RENDER_SKIP_20261021.md)。隔离5210 PID43488，不更新APK。 |
| 结算会话 | 已完成本轮结算修复：专项459、核心309969、客户端1828、浏览器31项通过，Web/Android Compile零警告错误。详见[结算记录](../reverse/VICTORY_SETTLEMENT_20261021.md)。隔离5197 PID35204，无新APK。原生随机掉落未补入，获得栏使用当前累计记录。 |
| 最终结果投影 | 可选 `battleResult`：id/scenario/outcome/round/stars/minimumRatingRound/highestRatingRound/awardMedals/collectedMedals/name/nextScenario/generals。门槛取btl，评分/战役差额奖励与征服奖励有原生向量；GUID局身份、账户凭据及历史最佳防重付，旧档字段可缺省。 |
| 前端同步入口 | `victory-ui.js` 的 `Results.sync(state)`，实例 `victoryUi` 在 `refreshHud()` 末尾同步。等待首帧/加载/当前图片就绪再开面板；关闭去重，新局重置。性能路径保持结束边界完整安装及绘制唤醒。 |
| 共享文件改动 | 结算仅窄改 ClientGame.Start/Load/InstallSession/View、ClientServices.AiCombat、app.js初始化/refreshHud/重开/面板比例复位及index加载；新增独立模型/模块/专项。WorldAi候选/移动后攻击补Finished/对白守卫，防止胜负已定后攻击报错。普通/批次最终结果及完整存档字节一致。 |

性能会话接口（已验证）：`action {op:'ai_batch',revision}` 返回 `{state,baseRevision,steps,error}`。最多32步且每步后检查250ms预算（单步规则本身不可中断），逐步调用原 `Commit`，不合并规则/存档/结算。`state` 保留 `View` 的结果字段；执行过步骤时 `cells:null`，`cellUpdates:[{cell,value}]` 是相对请求 revision 的地块增量，其他状态字段完整。前端按 baseRevision 合并地块，跳过中间 HUD/演出，只在己方/对白/选项/结束/错误边界走完整 `installState` 与 `refreshHud`，保留结算会话新增 `victoryUi.sync(state)`。错误响应携带最新已提交状态；网络错误另外请求 `op:'state'` 恢复同步。普通 action/state/start/load 仍输出完整 cells。

新会话可直接以“读取本文件，按其中分工补入胜利结算”开始；当前会话继续负责渲染与跳过加速。
