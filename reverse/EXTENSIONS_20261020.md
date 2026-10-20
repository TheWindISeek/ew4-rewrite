# 开放规则 API 1 交付记录

日期：2026-10-20。接续 EXTENSIONS_WIP_20261020.md；本记录覆盖其中待完成的源码、指南与验证。未更新APK。

## 已实现

- 包根rewrite/extension.json显式注册技能/特性/绑定/布局/Lua/胜负条件，schema与apiVersion均为1；无入口保持原规则。
- 兵种标签、主动/被动技能；将领技能数量不固定，可配置等级、顺序、实例ID及坐标；列表/网格/定位和滚动说明。
- 后端技能事务验证回合、修订号、目标、范围、费用及冷却；按实例保存状态，统一死亡/将领退场处理。
- 原技能被同ID覆盖时停用旧效果，包含原经验与继承加成，保留扩展实例；不同时叠加两套行为。
- Lua离线解释、确定性随机、只读单位/地块/国家快照、实例整数状态及VM指令预算。导入检查模块初始化和函数引用。
- 自定义胜负支持组合、国家/单位/地块、红点、回合、资源及Lua；可设置同时满足的优先级、回合上限和玩家存活要求。初始化后即检查，后续事务/回合再检查。
- BTL VICT v1来源绑定；与JURI共存，优先于Mod胜负配置；CLI victory-export/apply/strip，保留原主体/其他块。
- JSON空值/重复键/未知字段/嵌套预算及BTL脚本依赖校验；扩展参与Mod规则指纹，原版/各Mod/逻辑版本保存隔离。

## 制作资料

- 完整协议/参数/事件/函数入口：rewrite/mod-kit/docs/porting/RULE_EXTENSIONS.md。
- 可打包示例：rewrite/mod-kit/examples/rule-extension（8个运行文件加README）。法国一级重炮修理、标签、七技能将领、网格布局和征服回合胜利。
- 工具包各导航、配置/源码/原包对照/排错及资源目录README已补链接和边界说明；Lua许可随Web/工具分发，Android加入许可资产。
- build/ew4-mod-kit已同步：1977文件，185988857字节。未新增APK或提交重复安装包。

## 验证结果

| 范围 | 结果/证据 |
| --- | --- |
| 完整核心 | 309638项，含扩展43项，0错误；rewrite/reports/core_validation.json |
| 客户端回归 | 859项，0错误；Ew4.Verify --client-only |
| 既有Mod导入回归 | 77项，0错误；rewrite/reports/mod-import-20261019/results.json，本轮run=47355c720d37448784ee4f31648fbca1 |
| 扩展离线Mod | 18项，0错误；rewrite/reports/extensions-20261020/latest-import.json。原格式+rewrite目录、assets ZIP、封包往返、主动API/存档/冷却、七槽和原版恢复 |
| 分发示例与VICT CLI | 9项，0错误；rewrite/reports/extensions-20261020/cli.json。示例全内置剧本验证、导入/导出/重导，VICT写回/导出/撤销字节一致 |
| 浏览器 | 20项、6图，异常0/缺失0；rewrite/reports/extensions-20261020/web.json。独立5185，桌面1136×640/横屏844×390；实际离线导入、技能说明、滚动、主动费用/冷却保存，分发示例七技能网格及原版恢复；画布像素非空 |
| Android | -t:Compile，0警告/0错误。显式指定.codex/tools/android-sdk与android-jdk；未做实机运行或生成新APK |

浏览器验收脚本tools/verify_extensions_web.cjs对异步导入使用Node侧轮询并读取完成包key，避免Promise假值被当成等待完成。CLI脚本tools/verify_rule_extensions_cli.ps1可复跑，生成物位于独立reports目录。

## 预览与存档

- 5184已更新到rewrite/local/extensions-delivery-runtime，PID36912；更新前立即检查用户状态，无活动战局、原版启用；更新后仍为空会话/原版。
- 1741个已有档案/内容文件逐项哈希保持一致；未读取历史快照覆盖当前进度。证据rewrite/reports/mod-import-20261019/preview-preservation.json。
- 前端app/skill-ui及样式缓存版本20261020.3；刷新5184即可使用新导入接口。
- 自动化只操作独立5185与rewrite/local/extensions-validation-profile，不操作用户对局。5185验收结束后停止。

## 明确边界

- 不是执行原Mod SO/DEX/DLL，也不自动翻译未知原生技能。原XML的未知编号须移至扩展定义/绑定；API外的行为仍需支持版本。
- 当前支持的事件/操作以指南表为准；未提供任意世界写入、创建单位、建造资格或完整UI脚本接口。
- AI应用共享被动技能，但尚不规划自定义主动技能；数值修正/胜利Lua为无状态查询，只有普通事件实例状态持久化。
- 指令预算不是进程级内存隔离；不要构造巨型Lua表/字符串。不能据专项和内置示例通过声称所有真实Mod或整局已验证。
- Android仅编译验证；旧安装包不会因电脑代码更新自动支持API 1。
