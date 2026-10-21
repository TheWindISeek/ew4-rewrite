# 周期事件与独立双边外交

日期：2026-10-21。用户确认补齐两项通用接口，不在原剧本自动添加玩法。本轮源码、作者文档及可选示例已完成；5192 预览未重启，未更新旧 Web 发行包或 APK，未操作玩家存档。

## 接口与行为

- ScriptedScenarioEvent.repeat：everyRounds/startRound 默认 1，endRound/maxOccurrences 可选。按整轮固定计划检查，同一事件同轮最多触发一次；条件整轮不满足则跳过，不补扣。直接结果和多按钮弹窗均可周期运行。
- WorldState.ScenarioEventProgress 持久保存次数与最近回合。旧 TriggeredScenarioEvents 仍为唯一的曾触发 ID 列表；重复弹窗下次展示清除旧选择，保存最近选择。event_count 条件与 ctx:event_count 支持计次；展示弹窗即计一次，event_completed 在新待选弹窗期间为假。
- insufficientResourcesEffects：任一资源结果将透支时撤回全部原结果及其通知，再从原状态执行短缺结果；弹窗公共结果与选择结果为同一事务。默认仍拒绝透支，其他类型错误不兜底。短缺结果本次成功后占用本轮触发次数，防止不停重试。
- ScenarioCondition/ScenarioEffect.type=relation，faction/otherFaction 接受实际国家 ID、@player/@active。关系 allied/neutral/hostile；结果还支持 default，撤销该对覆盖及原中立开战记录。
- 双边关系对称、不传递，不改 camp，也不改变第三国关系；显式关系优先于后续 camp 修改，直到 default。中立 AI 不主动交战，玩家主动攻击显式中立会使该对关系变为敌对。
- WorldDiplomacy.Set/Relation/Allied/Hostile 为共享入口。部队攻击、AI 战略/战术、移动通行、邻接治疗、光环/溅射、辖区保护、默认与自定义胜利共同遵循关系。按 camp 的普通数量条件仍统计字面阵营。
- ClientContent.Start 保存中立阵营策略；Lua ctx:relation 读当前关系。客户端 factions[].relation 投影到玩家视角，battlefield.js 攻击标记与蓝/黑/红国旗采用此字段，兼容旧响应的回退行为保留。
- WorldSave4 和 SessionSave2 沿用版本号，新状态字段默认空，旧无此字段存档已验证；扩展配置仍进入规则指纹，不绕过逻辑版本隔离。

完整作者指南：rewrite/mod-kit/docs/porting/SCENARIO_EVENTS.md；根 README、配置/源码索引和示例索引同步。独立可选示例：rewrite/mod-kit/examples/scenario-recurrence-diplomacy，演示出征周期军费、AI 三资源补给、短缺撤回和第 4 回合外交选择。不会自动加载进已有内容。

## 验证与产物

报告目录：rewrite/reports/scenario-recurrence-diplomacy-20261021。

- 新接口专项 102 项，整体事件专项 207 项、0 错误。覆盖周期边界/上限、跳过/恢复、真实多国回合、短缺死亡和扣款整体撤回、重复弹窗及读档、陈旧命令、无效配置/进度、三国独立关系、默认恢复、中立开战、同阵营攻击、跨阵营盟军、AI/移动/胜利、Lua、旧存档缺省、真实客户端 JSON 加载与存读档。
- 完整核心 309845 项、0 错误，结果保存在 full-core.json；客户端 862 项、扩展 43 项、0 错误。verified-runtime 保存该完整回归对应程序集，避免共享目录其他任务的后续改动使证据混淆。
- 前端关系投影 24 项、0 错误，tools/verify_scenario_diplomacy_ui.cjs 验证关系优先于同/异阵营、单位/地块目标与旧响应回退；ui.json 落盘。battlefield.js 语法通过，本轮没有新增布局，未重复进行弹窗截图验收。
- Web 编译和专用 Android dotnet -t:Compile 均 0 警告/0 错误；Android SDK/JDK 位于 C:/Users/WIN11/.codex/tools。共享目录同期技能接口编辑曾短暂造成编译阻断，待其字段衔接后重编译成功；最新源码事件专项再次通过 207 项。本记录不代替另一任务对新技能的验收。
- 使用隔离工具运行目录完成示例 pack/import/export，导入无警告且未启用。产物 scenario-recurrence-diplomacy.ew4mod，SHA256 f58cf1fa4f839fb2ed761138f6119e9e33642e31c1e7c55f45260f8731fc330c，需使用包含本轮增补的宿主及匹配基础资源。

## 预览边界

5192/PID11500 仍使用 rewrite/local/scenario-preview-20261021/runtime 的旧独立程序集和已缓存基础指纹。本轮未更新或重启；新示例与其基础指纹不匹配，不能直接当成该旧预览可用包。以后更新预览时需保留玩家最新状态，并按届时基础资源重新生成兼容测试包；不可回滚存档或手改清单绕过校验。

运行专项：dotnet run --project rewrite/Ew4.Verify -- E:/Workplace/game/o4mod --scenario-only。前端投影：node tools/verify_scenario_diplomacy_ui.cjs。未生成 APK、未设备运行、未进行整局通关。
