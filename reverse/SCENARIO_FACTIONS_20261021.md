# 剧本事件势力创建、复国与身份修改

日期：2026-10-21。源码接口已实现，独立 Web 预览已验证；未更新既有 0.2.6 发行包、打包新 APK 或进行 Android 真机验收。

## 接口与行为

- `faction_create`：现有 `scenarioEvents` 的普通结果。条件满足后可由事件 `effects` 直接执行，也可由 `popup.choices[].effects` 在选择按钮后执行；无固定延时要求，等待弹窗时不提前创建。
- `faction_revive`：恢复已存在且已灭亡的势力，不自动恢复旧部队、死亡将领和领土。
- `faction_identity`：分别或同时修改 `name`、`color`、`flag` / `flagImage`，保持运行 ID、玩家归属、阵营及外交关系。
- 新增 `faction_exists` / `faction_active` 条件，区分尚未创建、已存在和已灭亡。

创建时 `templateFaction` 可省略；`unitVariant` 可引用 Mod 在 `legacy/catalog.json` 登记的新变体，招募卡可显式配置。旗帜可用新增图集前缀，也可用包内 `rewrite/*.png` 独立图片；颜色支持 `#RRGGBB` / `#RRGGBBAA`。独立图片为静态旗面，沿用现有 HUD 可见性。无模板创建须指定名称、阵营、旗帜和颜色。

新势力作为 AI 追加到回合顺序，按正常回合交接行动，不自动获得资源、部队或城市。可在同一结果数组依次创建、授予资源、转交部队及地块、调整外交；整组结果原子提交，资源不足兜底会撤回创建及身份修改。重复创建拒绝，未创建目标不会误匹配条件。新势力与显示修改均可存档；原剧本缺少新字段时保持兼容，事件规则变更仍需匹配存档指纹。

完整字段及示例见 [势力事件指南](../rewrite/mod-kit/docs/porting/SCENARIO_FACTIONS.md)、[事件接口](../rewrite/mod-kit/docs/porting/SCENARIO_EVENTS.md)及[可选作者示例](../rewrite/mod-kit/examples/scenario-factions/README.md)。作者示例没有机器绑定的生成清单，封装副本保存在报告目录。

## 验证

| 范围 | 结果 |
| --- | --- |
| 新增势力专项 | 56 项，0 错误；`rewrite/reports/scenario-factions-20261021/core.json` |
| 当前源码事件检查 | 263 项，0 错误 |
| 当前源码客户端检查 | 1790 项，0 错误；包含共享工作树当前检查集，不据此宣称其他任务完整验收 |
| 完整核心检查 | 本功能实现阶段通过；后续共享源码变化另以事件专项及当前构建确认 |
| Web 与 Android Compile | 当前共享源码构建通过，0 警告、0 错误；未生成新 APK |
| 浏览器 | 17 项通过，页面错误 / 缺失资源均为 0；`rewrite/reports/scenario-factions-20261021/web.json` |
| ModTool | 测试包与作者示例封装、校验通过，覆盖 95 个基础剧本 |

浏览器检查覆盖真实导入启用、直接结果、弹窗创建及拒绝分支、待选和创建后存读档、独立新兵种键、改名换色、部队/地块转交、自定义 PNG 旗实际绘制及非空画布。桌面和手机横竖屏截图均已查看，见报告目录 `pending-desktop.png`、`created-desktop.png`、`created-mobile-landscape.png`、`created-mobile-portrait.png`。JavaScript 语法及 diff 空白检查通过。

## 隔离预览与交付边界

- 预览：<http://127.0.0.1:5207>，交付时进程 PID 40264，测试包已启用，可从第一征服法国开局查看弹窗。
- 独立运行时：`rewrite/local/scenario-faction-preview-20261021/runtime-current`。
- 固定前端副本：`rewrite/local/scenario-faction-preview-20261021/site`，避免其他会话继续编辑源码影响本次验证。
- 独立存档 / Mod：`rewrite/local/scenario-faction-preview-20261021/saves`。
- 日志：上述预览目录的 `current-stdout.log`、`current-stderr.log`。
- 测试证据与已封装示例：`rewrite/reports/scenario-factions-20261021/`。

保留并兼容共享工作树内渲染 / AI 批处理及胜利结算改动；这些改动由对应任务继续验收。其他玩家服务及存档未操作，无发布、无新 APK；旧 0.2.6 不包含本接口。
