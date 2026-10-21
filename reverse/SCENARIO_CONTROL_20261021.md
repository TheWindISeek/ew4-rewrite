# 事件角色切换与动态胜负条件

2026-10-21：已实现源码、作者指南及可选示例。始终是一个玩家转换所扮演的势力，非增加玩家；原控制国交给 AI，新控制国按正常回合顺序接手。

## 交付接口

- `player_control`：指定已创建、未灭亡国家，切换唯一人类控制势力、会话权限、HUD/外交视角、名册派遣及后续 `@player`。不跳转回合，不重置资源/行动次数，不自动转交部队、领土或外交。
- `player_faction`：条件判断当前所扮演势力，与正在行动的 `active_faction` 区分。
- `victory_rules`：结果字段 `victoryRules` 使用既有 ExtensionVictoryRules 完整替换本局胜负条件，保存为 WorldState.ScenarioVictory，所有 WorldVictory 判定路径生效。
- `victory_default`：撤销事件覆盖，恢复原本安装的剧本规则。
- `ending`：指定 `key` 分支 ID 与 `outcome: victory/defeat`，结束本局，WorldState.ScenarioEnding / 客户端 ending 保存标识；仍通过既有结算防重复，不额外生成奖励和文案。

以上结果支持现有条件直接触发或选择按钮执行，可与势力创建、驻军/地块转交、资源及外交组合，整组原子提交；资源不足兜底会撤回角色和胜负修改。目标尚未创建、已灭亡、对局已结束、无效规则/结局均拒绝。尚未创建的胜负目标不会算成已灭亡。

`@player` 在普通查询/结果中指当前控制国；创建声明的 `templateFaction: @player` 在客户端装载时绑定开局玩家模板，避免切换后显示/招募来源改变或引用自己。SessionActions.SyncPlayer 同步权限和派遣记录，原历史保留；没有目标记录时沿用前国名额上限。重新开始使用原始选国，镜头切换到新控制国。旧无新字段存档继续可读，LegacyScenarioHost 指纹跳过空新字段。

完整字段及两场景说明见 [SCENARIO_CONTROL.md](../rewrite/mod-kit/docs/porting/SCENARIO_CONTROL.md)，可封装实例见 [scenario-control](../rewrite/mod-kit/examples/scenario-control/README.md)。事件接口、势力接口、工具包入口/配置/源码索引已同步。

## 验证

| 范围 | 结果 |
| --- | --- |
| 控制与胜负专项 | 61 项，0 错误；`rewrite/reports/scenario-control-20261021/core.json` |
| 当前事件全套 | 324 项，0 错误，含原有事件/周期/外交/势力回归 |
| 完整核心 | 本次实现阶段全套通过，所有输出错误计数为0；之后补充作者示例检查后事件专项再次通过 |
| 客户端 | 1849 项，0 错误；共享工作树当时检查集，不替代其他任务完整验收 |
| 扩展 / 通用技能 | 43 / 68 项，0 错误 |
| 浏览器 | 15 项，页面错误0、缺失资源0；报告 `rewrite/reports/scenario-control-20261021/web.json` |
| 构建 | 独立输出 Web 构建与专用 Android dotnet `-t:Compile` 均0警告/0错误 |
| 作者示例 | ModTool封装/校验通过，95个基础剧本；源码示例未放机器绑定清单 |

专项实测两种来源：本国驻军/地块分离、进攻方转投外国叛军。覆盖选择继续原国、选择扮演新国、无弹窗直接切换、待选/切换后/结局存读档、权限/派遣历史、正常回合交接、胜负替换/恢复、资源不足回滚、结局胜败、嵌套 Lua 和作者实例第5回合结局链。

浏览器真实导入启用封装包并点击两个选择分支，实测前国 AI 正常完成回合并轮到新国、HUD切换、待选恢复、胜利面板及读档不重复发奖。桌面/手机横竖屏弹窗与非空画布检查通过，截图已查看：`choice-desktop.png`、`role-desktop.png`、`role-landscape.png`、`role-portrait.png`、`ending-desktop.png`。最终前端含切换镜头及原国重开修复，浏览器再次通过。JS语法及diff检查通过。

## 隔离预览

- 地址：<http://127.0.0.1:5212>，PID 16652。
- 运行时：`rewrite/local/scenario-control-preview-20261021/runtime`。
- 固定前端：该目录的 `site/wwwroot`。
- 存档 / Mod：该目录的 `saves`；日志 `stdout.log` / `stderr.log`。
- 测试包、作者示例封装及报告：`rewrite/reports/scenario-control-20261021/`。

默认Web输出被另一进程占用，已改用独立输出构建，不停止该服务。系统dotnet无Android工作负载，已使用现有专用工具链编译通过，无安装机器级依赖。未操作其他玩家服务/存档，共享渲染、商店和结算改动保留。未打包新APK、发布或真机验收；旧0.2.6不含本增补。
