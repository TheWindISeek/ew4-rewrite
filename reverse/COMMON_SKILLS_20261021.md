# 通用技能 API 2

## 交付

按用户要求为通用技能补后端接口，不逐个硬编码Mod技能。保留schema 1和API 1兼容，新增API 2：地块目标、召唤、传送、目标/自身/范围效果、持续状态、当前移动力、移动上限与受击减伤。兵种和将领共用技能定义与Lua上下文。

接口主要位于 `rewrite/Ew4.Core/RuleExtensions.cs`、`SkillWorldEffects.cs`，加载与沙箱位于 `Ew4.Legacy/ExtensionLoader.cs`、`ExtensionScriptRuntime.cs`。前端已接地块选择与状态显示。主动动作一次费用/冷却/修订，任一范围目标计算失败则整体不提交；持续伤害共用死亡/阵营出局与对白暂停流程。状态随受影响方回合结束递减，可保存，支持refresh/replace/stack。

召唤使用注册兵种模板、普通永久单位，遵守海陆/容量/归属限制且初始不可行动；传送仅自身到友方/无主合法格，不占领。Lua返回数值与受限整数计数，通过声明式操作改变世界，不执行包内原SO/DEX/DLL。AI接入被动与状态，尚不主动选择自定义主动技能。

## 指南与示例

- Mod修改指南：`rewrite/mod-kit/docs/porting/README.md`。
- 完整字段及语义：`rewrite/mod-kit/docs/porting/RULE_EXTENSIONS.md`。
- 六技能示例：`rewrite/mod-kit/examples/common-skills/`，法国一级重炮绑定召唤、传送、范围炮击、群体急行军、叠层中毒、解毒；可改为commanderBindings。
- 示例导入报告：`rewrite/reports/common-skills-20261021/latest-import.json`，包含实际可导入`.ew4mod`路径。

## 验证

- 最终完整核心回归309913项通过，`rewrite/reports/core_validation.json`，errors=0；API 1专项43、API 2专项68、事件207；客户端862项通过。
- 既有扩展Mod检查18，新通用技能Mod检查18，均通过。
- 浏览器26项、9张桌面/横屏/竖屏截图，异常与缺失资源0；含真实导入启用、召唤、传送、范围选点、状态存读档与画布检查。
- Web及Android Compile零警告零错误；JS语法与定点diff检查通过。APK设备执行未做。
- 测试工具：`rewrite/Ew4.Verify/CommonSkillChecks.cs`、`rewrite/Ew4.ModChecks/CommonSkillModChecks.cs`、`tools/verify_common_skills_web.cjs`。

## 独立预览

`http://127.0.0.1:5206`，PID30348，存档及Mod独立位于 `rewrite/local/common-skills-preview/`。未重启玩家服务。旧发行包需升级才能使用API 2；本轮后续0.2.6发布见 `rewrite/RELEASE_0.2.6_20261021.md`。

## 0.2.6发布准备

用户随后要求同步Git并上传Web/APK到Release。产物统一位于 `build/release-0.2.6-20261021/`，附件位于 `assets/`，自包含Web目录为 `web/`。包名为 `ew4-rewrite-0.2.6-20261021-web-win-x64.zip` 与 `ew4-rewrite-0.2.6-20261021-arm64.apk`。发布地址为 `https://github.com/aztksilence111/ew4-rewrite/releases/tag/v0.2.6-20261021`，上传结果及精确提交/哈希以该目录 `release-manifest.json`、`release-remote.json`、`remote-audit.json` 为准。

Web独立运行于5210、独立test-saves/test-mods完成26项真实技能浏览器检查；分发目录2570项资源/字体/指南/示例/依赖检查通过，不包含玩家存档。APK版本0.2.6/code8，沿用0.2.4签名，资源/程序集、v2/v3签名及16KiB对齐核验。未做Android设备运行。源码同步使用Git凭据与GitHub API，保持本地和远端commit/tree标识一致；不上传无关的民国Mod作者目录。
