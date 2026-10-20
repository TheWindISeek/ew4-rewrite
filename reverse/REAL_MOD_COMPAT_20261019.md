# 工作区真实 APK 兼容实测

**后续修复已完成一轮，当前先读 [资源解析与一次性飞弹](MOD_RESOURCE_COMPAT_20261019.md)。** 最新225347报告中原版、皇凯、一战、大鏖战通过，剩大前线和君临天下失败。下文215416为保留的初始失败证据，不是当前状态。

## 结论与当前断点

2026-10-19 对项目根目录全部六个APK实测。**原版通过，五个改版仍无法完整导入运行。离线导入入口已实现不等于现有Mod已兼容。** 前次33项构造样本及原包启动验证不能支持“常见改版已经兼容”的结论。本轮修复了解析/转换问题，但没有将五个失败结果改称通过。

最终机器报告：`rewrite/reports/real-mod-compat-20261019/20261019-215416/results.json`。每包记录源APK SHA256/字节数、步骤、耗时、成功检查或失败原文；子目录有详细异常。测试进程遇任一失败返回非零退出码。

| 文件 | 最终结果 | 仍阻塞的证据 |
| --- | --- | --- |
| 大鏖战 老友版_1.6.6(1).apK | 失败：目录规则验证 | def_army.xml的Siege Artillery多个国家/编队strength=-999，当前UnitProfile要求正生命值；包内另有libO4s1.so，特殊数值含义尚未恢复 |
| 大前线_1.6.6(1).apK | 失败：XML读取 | def_commander.xml第984行commander缺少闭合，987行根标签结束时结构不匹配；另外资源中skill1～4出现40～50号技能，当前只实现0～39 |
| 皇凯字库补全版.APK | 失败：2个剧本开局 | campaign3_11部队cell2410、11号兵种、grade1、士气3超出现有-3～1范围；conquest4兵种/编队/国家组合无法映射 |
| 君临天下mod整合版.apk | 失败：将领规则 | Treville(id2401)的nobilityrank=12，当前爵位治疗表仅0～9；另有爵位10。需要配套治疗、商业、经验、培养规则，不能只截断数值 |
| 欧陆战争4 一战MOD64位适配.APK | 失败：27个剧本开局 | 涉及shortage=6、越界势力索引、未解析城市名、商店/酒馆引用、171/6的当前/最大生命、征服设施位置或重复项；全部剧本ID见机器报告 |
| 欧陆战争4_1.6.6.apk | 通过本轮范围 | 95剧本开局验证、启用、目录读取；战役/征服/教程代表场景行动及保存重载 |

皇凯、一战和原版的ARM64游戏SO完全相同：`C16905A2F6D155111A1FF702F2B0F8FBAD09482DCEDCE08785A2BF7965FE7F63`。因此这两包的失败不能笼统归因于“原生新技能不能移植”；其中存在rewrite对原版输入/状态支持不完整的问题。大鏖战、大前线的同名基础库hash不同，且前者另带扩展库；君临天下为旧32位/x86库。这里只读分析，不运行第三方SO/DEX。

## 已修的兼容层

- `Ew4.Mods/LegacyXml.cs::Normalize`：兼容version="1"声明、相邻引号属性、原读取器在未结束尾注释之前保留的完整子节点；仍拒绝结构不匹配、未完成CDATA及DTD。合法XML连同BOM、声明、换行保持原字节，防止基础源hash失配。
- `LegacyGraphics.cs::LegacyBinary.Font`：按头部count读取记录，允许原包改字库后残留的尾数据，拒绝截断头/记录。
- `LegacyModImporter.cs::Convert`：重复图集键保留末项；空UI矩形使用透明占位并记录提示；仅对实际UI图集生成裁剪图，兵种继续用原图集/动画，避免无意义的全图裁切失败。
- `ConvertProfiles/ConvertEffects`：处理重复动作/特效名称，避免ToDictionary直接抛重复键错误。
- `ConvertAnimations`：HD动画解析失败时，只允许同一输入包内完整SD动画与SD图集成对回退，并在报告明确记录来源；两者都无效仍失败。一战的strategicmap_hd.bin存在尺寸/块不一致，包内strategicmap.bin可解析。
- `Ew4.Legacy/LegacyScenarioEvents.cs::Import`：原生回合/死亡/占领分发只处理对应已知效果，其余非负效果仍可以触发对话。保留皇凯8号事件的对话，不伪造数值效果。证据为原生`0008d95c.c`等函数。
- `LegacyScenarioInitialization.Prepare`：同一国家允许两个独立索引势力。`LegacyScenarioEvents.Import`按原`00080b9c.c`顺序取第一个同名国家作为事件目标，不合并两支势力。
- `ModStore.Validate`：汇总所有剧本开局错误；增加部队、爵位及势力具体字段，避免每次只能看到一个模糊错误。

没有删除问题关卡/事件、替换新技能、改写负生命值、截断爵位，也未将损坏HD动画替换成其他包的素材。失败包不安装、不改变当前启用内容。

## 验证范围

`RealApkChecks.cs`通过真实ModStore执行导入/启用。原版代表场景为campaign1_01、conquest1、tutorials1：

- 战役：攻击、移动、招募、结束回合、首个AI步骤、保存/重载状态一致。
- 征服：移动、结束回合、首个AI步骤、保存/重载；开局无合法攻击目标、无可负担招募，因此这两项明确跳过。
- 教程：移动、招募、结束回合、首个AI步骤、保存/重载；开局攻击跳过。
- 不是全战役通关、完整AI整轮或长时间稳定性验收。失败五包尚未进入上述行动检查。

浏览器隔离5185：`tools/verify_real_mod_ui.cjs`，6项、0异常/缺失、4张图，保存于`rewrite/reports/real-mod-ui-20261019/`。用真实原版APK安装内容验证菜单/战场，桌面画布9125采样点非透明、3049种颜色；有桌面及844×390截图。上传真实大前线APK验证错误可见、失败后当前安装及启用选择不变。没有对五个失败Mod作画面通过声明。

原功能回归：核心309349项0错误；共享导入45项0错误（包括合法XML字节/hash不变、TinyXML兼容、残尾FNT、截断/DTD拒绝）；这两个数字不能替代真实六包结果。共享报告在`rewrite/reports/mod-import-20261019/results.json`，核心在`rewrite/reports/core_validation.json`。

运行命令：

```powershell
& 'C:/Users/WIN11/.codex/tools/dotnet-android/dotnet.exe' run --project rewrite/Ew4.ModChecks -- 'E:/Workplace/game/o4mod' --real-apks
# 可在--real-apks后追加文件名片段作定向回归。
```

## 文档和发布

`rewrite/mod-kit`扩充为19份README及5份补充说明，涵盖根目录、data、original、legacy、frontend、client及UI/图集/字体/动画/地图/音效、mods、saves、examples。`docs/CONFIG_REFERENCE.md`列出具体配置节点，`docs/SOURCE_INDEX.md`定位C#/JS函数，`docs/TROUBLESHOOTING.md`对应实际错误。48个相对文档链接及46个源码路径核对无缺失。

`tools/publish_mod_kit.ps1`最后复制详细文档，防止运行数据内旧字体README覆盖新指南；Web发布排除同名旧README并包含docs。最终工具包为1962文件、185336512字节，约185.34MB；本轮未重打APK。

5184已切换到`rewrite/local/mod-compat-runtime`，PID29784。始终保存切换当时当前局：conquest1 revision0、252单位，重载9项一致、核对5个存档文件；不能据此覆盖用户以后进度。证据在`rewrite/reports/mod-import-20261019/preview-preservation.json`。5185测试服务已停止；无手机或模拟器操作。

## 下一阶段

先针对同原版SO的皇凯/一战，用原生探针核对非标准士气/粮食状态、兵种映射、缺标签和重复/边界设施的处理方式，再改兼容导入策略；每类用上述真实剧本回归。随后分别恢复大鏖战特殊数值与大前线新技能、君临天下爵位表。不要只放宽世界校验来制造“导入成功”。当前断点是这些规则/状态兼容，尚未完成。
