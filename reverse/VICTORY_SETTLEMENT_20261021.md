# 胜利结算修复与验证

日期：2026-10-21。仅处理 HANDOFF 分配的 FB-08 与关联 FB-02，不承接渲染优化、商店或其他反馈。

## 已实现

- 复用现有 `form_victory`、原版星级/门槛板、勋章、按钮、将领头像与离线文字，补齐实际关卡、完成回合、五颗星、两个门槛、奖励、获得、已派遣将领和空槽。
- 胜利提供继续/退出，失败提供重开/退出；战役继续进入下一个可访问且匹配国家分支的关卡，征服/末关继续返回主菜单。修正战役重开错误传入征服国家参数的问题。
- 竖屏按面板宽度放大，横屏沿用现有比例，按钮与将领槽不越界。结果先等待加载完成、实际战场帧及已请求图片就绪，避免结束存档一打开就遮住首帧而留下黑背景。
- 后端结算在已提交 Finished 状态且对白完成后执行；普通与批处理均经过同一入口。刷新、关面板和按钮不发奖。
- 已派遣自有将领的军衔/爵位成长写回账户，取较新进度，避免旧局恢复覆盖更高成长；面板读取参战运行时的军衔/爵位。

## 原版规则证据

`tools/reverse/capture_victory_settlement.py` 执行原 ARM64 `0x7eeb8` 和 `0x7ef38(false)`，外围账户/资源读取被拦截。原件未修改。结果在 `reverse/reports/victory_settlement_vectors.json`，342 组评分、42 组征服奖励。

- 战役门槛来自实际 `.btl` 的 Header[12]/Header[13]，对应原 Battle +0x50/+0x54。不是布局示例或参考 Mod 的 13/10。
- `round <= highest` 得 5 星；`round >= minimum` 得 1 星；中间取 `max(2, 1 + floor(4*(minimum-round)/(minimum-highest)))`。包含门槛相等与最高门槛为零的向量。
- 原数据表 `0x196fc0` 为 `[0,25,50,75,100,150]`。战役只发本次星级价值超过历史最佳的差额；同章同关的 alternate 共用历史最佳。
- 征服按原 `0x7f300` 使用的 `0x7ef38(false)` 分支，读取实际回合、总部前十二名将领军衔/爵位、玩家资源及累计战役星级，通过原阈值表换算勋章。原版征服失败也可产生这种评分收益；它与战役胜利奖励不同，战役失败奖励始终为零。大资源值用 decimal 中间值避免 long 溢出。
- 未为结算显示增加战斗随机抽样。原 `CollectMedal` 在 `0x825d4` 的战斗掉落分支更新，现有现代战斗尚无该分支。本次 `CollectedMedals` 只保存/展示已记录累计值，当前正常局为 0；不伪造 40、不按击杀数冒充掉落，也不宣称已恢复原生随机掉落。恢复掉落需单独核对随机调用链。

## 触发问题

实际移动 API 已覆盖最后红目标占领后生成 Victory、Finished 和 `battleResult`。征服保留原来的消灭条件；未统一改成占完红圈，也未绕过可配置胜负条件。

专项同时发现并修复：AI 战术候选预演、移动后攻击执行及客户端攻击预览在移动已结束战局时，仍会继续进入攻击，从而抛出 `action.phase`、丢失整条结果。现在遇到 Finished 或待处理对白立即停止后半段。回归证明普通与 `ai_batch` 的最终结果、勋章、随机状态及自动存档字节相同。

这不能代替用户原先未知关卡/未知 APK 版本的完整复现；没有证据支持放宽所有关卡条件。

## 数据与恢复

- `GameSession` 新增可选 `BattleId`、`BattleResult`、`CollectedMedals`；SessionSave 保持版本 2，缺字段使用默认值，旧世界存档版本/内容指纹不变。
- `PlayerAccount` 新增 `CampaignStars` 与按局 ID 保存的 `BattleResults` 凭据，同时继续保留原黄圈隐藏关解锁。
- 开新局生成 GUID 并在开局自动存档中保存。结果 ID 不依赖 revision 或单一关卡 ID。
- 先原子写账户奖励与凭据，再安装会话/写自动档；即使恢复结算前的手动档，已存在的账户凭据也阻止第二次奖励。同局重走到不同结果时展示当前结果，奖励为 0。
- 无局 ID 的旧档以 content/scenario/player 派生保守迁移身份。旧格式本身无法区分同关同玩家的多个历史局，因此这些旧局共用一次凭据；重新开始后获得正常独立 GUID。
- 黄圈解锁仍调用原 `CampaignProgress.Settle`；没有修改其规则。旧档兼容、已结算/结算前快照恢复、较低星级重玩、失败、重开均有专项覆盖。

## 集成接口与文件

`ClientGame.View()` 的可选 `battleResult` 包含：`id,scenario,outcome,round,stars,minimumRatingRound,highestRatingRound,awardMedals,collectedMedals,name,nextScenario,generals`。将领包含 `id,name,image,rank,title,withdrawn`。

前端独立 `victory-ui.js` / `victory-ui.css`，入口为 `victoryUi.sync(state)`，在 `refreshHud()` 最后调用。`sync` 可重复执行，已关闭结果不会因刷新再弹；新局无结果时重置。等待显示期间会使用最新投影对象，避免重复安装状态后一直等待旧对象。`ready` 只等待呈现，不决定胜负/评分/奖励。性能路径必须保留结束边界的完整安装、绘制唤醒与 HUD 同步。

共享源码只作窄补丁：ClientGame 的 Start/Load/InstallSession/View，ClientServices.AiCombat 的结束守卫；app.js 的模块初始化/refreshHud/重开/通用面板比例复位；index.html 加载项。额外核心守卫在 WorldAi.TacticalMoveCandidates/ExecuteMove。未重写性能会话的循环、动画、增量合并或批处理。

## 验证与预览

- 结算专项 459 项，错误 0；核心 309969 项、客户端 1828 项，错误 0。客户端数字为本会话运行时的代码快照，其他会话后续检查可能增加。
- Web 构建、Android ARM64 `Compile` 均为 0 警告/0 错误。使用独立 `rewrite/local/victory-build` 与 `victory-android-build`，未生成/替换 APK，未操作设备。
- JS/Python 语法检查与 `git diff --check` 通过。
- 浏览器 31 项，异常/缺资源 0；1280x720、844x390、390x844 覆盖边界、按钮、离线文字实际像素、非空战场背景、将领头像、关闭防重弹、继续/重开/退出。
- 证据在 `rewrite/reports/victory_settlement_20261021/`，含 checks.json、browser-checks.json、五张截图和隔离 profile。复核入口：`--settlement-only`、`tools/verify_victory_settlement.cjs`。
- 独立预览 `http://127.0.0.1:5197/`，本次 PID 35204。账户路径由 checks.json 的 profile 指向；战役自动档保留专项结束场景，浏览器脚本使用该服务的隔离手动槽。启动前检查端口；未重启玩家服务或读取/覆盖玩家账户与存档。PID 是历史快照，后续须现场核对。

尚未真机运行；浏览器手机尺寸不是 Android 验收。原生随机掉落与用户原未知局的完整复现不在已验证范围。
