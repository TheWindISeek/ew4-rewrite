# 名册上将、公主重领与剧本事件

日期：2026-10-21。接续开放规则 API 1。源码与隔离 Web 预览已更新；没有重打 APK、更新旧发行 ZIP 或操作用户存档。同期字体/经验条改动由另一任务维护，保留原有更改。

## 将领与公主

- 定点复核原包 `reverse/native/decompiled/00091194.c` 和 `0005b260.c`：原界面检查部队空任职/派遣名额，动作 10 从名册挂接并登记历史；既有细节见 ROSTER_DEPLOYMENT_AND_TECHNOLOGY.md。
- 重写核心原有 RosterDeployment.Deploy。浏览器真实点击确认旧名册被战场 canvas 覆盖，导致可见画面不变且名册无法点击。Menus.headquarters/closeRoster 现在在战场上将期间正确切换战场和名册。
- 新增 `/api/deployment`，由 RosterDeployment.CanDeploy 查询该部队可用的局外将领，共用后端归属、位置、任职、历史及名额检查。不可用将领禁用出征。实际挂接后更新属性、锁定名册并自动保存，重载保留。
- 公主是否领取只看当前账户 Roster 中是否拥有相同 DefinitionId。ClaimedRewards 仅保留历史，不作阻止重领条件；整编后重新领取使用新实例 ID，避免冒用已消耗将领的历史身份。不能在仍拥有同一定义时重复领取；原容量限制保留。

## 剧本事件

- Manifest 增加 scenarioEvents，按剧本 ID 映射 ScriptedScenarioEvent[]。无配置不追加事件，原 BTL 回合/死亡/占领及对白继续执行。
- 条件树支持 all/any/not，六种数值比较；资源、城市/领土/黄圈数量、指定城市与地块、部队数量/位置/血量、击杀、死亡、将领任职/死亡、回合、国家出局/阵营、当前国家、事件变量与完成记录。Lua 可枚举地图、部队、国家、将领并查询状态。
- condition→effects 直接执行；condition→popup→choices 执行所选结果。按钮数量来自数组，支持每个按钮自己的条件、禁用、长文字和滚动。弹窗打开时不提前扣费；公共结果和所选结果在同一事务中执行。
- 结果支持三资源增减/设置、国家转阵营、地块/部队转交、士气/环境/扩散、治疗/死亡、整数变量、胜负。Lua 计算结果整数；不是任意世界写入或原生代码接口。
- 开局及共享提交、移动/战斗、回合前后检查；名册任命、酒馆和商店会话提交也进入事件管线。连续结果在一次宿主事务中规范化为一次世界修订，避免新增事件触发 session.world_revision 错误。
- 单位/将领真实死亡留历史，投降移除、卸任等退场不冒充死亡；如需任意消失用 not(unit_alive)。部队转交时任职将领随行，原派遣名额继续占用。玩家转阵营后胜负采用当前玩家国家的阵营。
- 复用 PendingDialogue/QueuedDialogues 阻塞行动和 AI；选择记录、配置、死亡历史和中断阶段写入 WorldSave4 新可选字段。选择不可通过普通 dialogue 确认跳过；重复选择/陈旧命令拒绝。事件导致的变化清除移动撤销历史。

全字段、限制、函数位置及制作方法：rewrite/mod-kit/docs/porting/SCENARIO_EVENTS.md。可打包中文示例：rewrite/mod-kit/examples/scenario-events。报告目录：rewrite/reports/scenario-events-20261021。

## 验证

- 完整核心 309734 项、0 错误，包含当时事件 96 项；随后补充的转交名册/原国名额/会话重载专项通过，最新事件专项 99 项。
- 客户端回归 862 项；既有扩展离线 Mod 导入 18 项；Web 编译与 Android -t:Compile（专用 dotnet-android/SDK/JDK）0 警告/0 错误。
- Playwright 17 项、6 图、页面异常 0、缺失资源 0。实际点击上将→自有公主→出征→自动存档恢复；真实离线导入并启用 12 按钮事件，验证禁用按钮/长中文按钮、横屏 844×390、竖屏 390×844、等待选择存读档和单一结果、Canvas 像素非空。截图与 web.json 在报告目录。
- 中文作者示例 CLI pack 验证全内置剧本、import 成功；导出 scenario-events.ew4mod 位于报告目录。没有把旧安装包标为支持新接口。

## 预览与恢复

- 隔离预览 http://127.0.0.1:5192，运行目录 rewrite/local/scenario-preview-20261021/runtime，账户 rewrite/local/scenario-preview-20261021/saves，当前 PID11404。运行 DLL 是独立副本，避免占用源码编译输出。
- tools/verify_scenario_events_web.cjs 只操作 5192 的独立测试档案；不可改为指向用户实际档案。同期另一任务 5193 未操作。
- 新事件相关单测命令：dotnet run --project rewrite/Ew4.Verify -- <root> --scenario-only。
- 没有全局安装 .NET workload；Android 使用 C:/Users/WIN11/.codex/tools/dotnet-android/dotnet.exe，并显式传 android-sdk/android-jdk。未生成 APK、未做设备运行或整局通关。
