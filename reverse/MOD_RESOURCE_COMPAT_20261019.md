# 离线 Mod 资源解析与大鏖战一次性飞弹

本阶段延续 `REAL_MOD_COMPAT_20261019.md`。原始 APK 不修改，不运行导入包的 SO/DEX，不生成新的 Android 安装包。

## 资源解析

`rewrite/Ew4.Mods/LegacyResourceResolver.cs` 统一处理资源优先级。输入包已有文件优先；缺少的共享 XML/INI 可以使用 rewrite 内置的原版数据。完整 APK 缺少其引用的 BTL 或地图 BIN 时仍报错，不能用原版剧本冒充成功。资源 ZIP 可以作为覆盖包使用。

缺城市名不再拒绝开局：`LegacyScenarioMap.Import` 保留设施、ID、归属与目标，仅让 `Name` 为空。可选补名仅在底图字节相同、同名剧本的格子/CityID/建筑类型均匹配且 Mod 没有定义该 ID 时使用内置原名；Mod 自己的名称及显式空名称优先。

`import-report.json` 的 `sources` 包含 `resource/source/sourceHash/resolvedHash/reason`，分别说明原包、内置回退、XML 规范化和补名来源。`nativeLibraries` 记录库哈希，`executed=false`。成功安装后的 `validation-report.json` 记录逐剧本地图诊断。规范化后的实际 XML 与生成目录的源哈希保持一致，原始输入哈希另存于报告。

## 原读取语义

- `LegacyBattle.EffectiveSites` 保留每格最后一条设施；地图、回合建筑、事件、客户端显示、黄圈计数使用有效记录。原始 `Sites/ToBytes` 保留输入全部记录。
- 物理地图内但剧本活动区外的设施保留元数据，不启用地形；物理地图外设施跳过。势力索引不存在时不编造国家。
- 商店、市场、酒馆记录保留物理格坐标，实际进入资格仍由运行世界判断。
- `LegacyRuntimeCatalog.SerializedUnit` 按 `unit*3+grade` 读取旧 BTL，支持皇凯炮兵 grade=3 指向下一兵种的原语义；正常招募定义仍是 0～2。
- 正士气和粮食短缺不再限制为原版常见上限，计算仍按原公式；旧生命超过旧上限时按加载链重新归一化并封顶。
- 初始负资源保留；后续加收入按有符号原值计算后下限归零。费用和收入本身仍须非负，不修改存档格式。
- 全局招募卡仅在该国存在对应兵种定义时生成招募、AI 和初始训练授权。缺少定义的显式 BTL 单位仍会报错，不借用另一国家属性。
- 恢复标志按非零为真；无到期时间的休眠行为字节原样保留。原头部目标开关按是否等于零判断。
- 重复事件按原 `8d6c0` 合并：保持第一次的位置、cell、triggered，更新最后的 condition/effect/round/dialogue/country。第零回合事件保留，但不会在第1回合误触发。

原生证据：`00051b2c.c`、`0007cb80.c`、`00080904.c`、`000a9744.c`、`00053cf8.c`、`000539b0.c`、`000702a4.c`、`00071014.c`、`0008d6c0.c`、`0008d95c.c`、`0005906c.c`。负资源/士气的隔离函数探针在 `tools/reverse/probe_mod_input_semantics.py`，报告 `rewrite/reports/mod_input_native_vectors.json`。

## 一次性飞弹

用户明确确认：大鏖战将攻城炮替换为飞弹，`strength=-999` 表示开火一次后消失。

`LegacyCatalogConverter.Convert` 将这个精确值转成正生命表示及 `consumed_on_attack` trait；内部基础生命为1，原数值仍保留于 `source_trees` 和输入 XML。其他负生命值继续拒绝。旧 BTL 的相同生命哨兵仅在该 trait 存在时转换，不放宽普通兵种的生命校验。

`WorldBattle.Transaction.ConsumeWeapon` 在本次主伤害和溅射完成后移除武器，沿用正常死亡事件、将领退场及势力存续检查，不把主动消耗记作敌方击杀。主动攻击者消耗；反击者仅在确实发生反击时消耗。被致命攻击先行摧毁时不反击、不重复移除。预览不消耗，非法命令不消耗，玩家和 AI 共用提交入口。

trait 随现有 profile 保存，开火前重载仍为一次性武器，开火后重载不会复活。前端先播放已提交的攻击，再移除旧画面单位；属性页将该负生命哨兵显示为“一次性”。

## 验证入口

- `Ew4.Verify/BattleCommitChecks.cs`：主动消耗、实际反击、未反击保留、致死阻止反击、无重复死亡、无虚构击杀、存档及事务回滚。
- `Ew4.ModChecks/LegacyResourceChecks.cs`：解析优先级、全包缺图拒绝、精确补名及哈希、哨兵转换、其他负生命拒绝、国家专属招募卡。
- `Ew4.ModChecks/RealApkChecks.cs`：真实包逐剧本启动与客户端投影，代表行动和保存重载；使用独立解锁测试账户覆盖隐藏关。另逐个检查导入的一次性 profile 的真实伤害、消耗和保存重载。
- `tools/verify_compatible_mods.cjs`：5185 独立存档上的实际改版贴图、桌面/手机尺寸、画布像素与异常检查。禁止对用户5184运行游戏事务测试。

## 最终结果

六包最终报告：`rewrite/reports/real-mod-compat-20261019/20261019-225347/results.json`。

| 输入 | 本轮结果 |
| --- | --- |
| 大鏖战老友版 | 108份剧本资源，102个单人剧本开局视图；三模式代表行动、存档重载和全部18个一次性profile通过 |
| 皇凯 | 97个单人剧本开局视图；三模式代表行动与保存重载通过 |
| 一战 | 124份剧本资源，逐单人剧本开局视图、三模式代表行动和保存重载通过 |
| 原版 | 95份剧本资源，逐单人剧本开局视图、三模式代表行动和保存重载通过 |
| 大前线 | 仍失败：将领XML结构错误及尚未移植的40～50号技能 |
| 君临天下 | 仍失败：爵位10/12与现有规则表不匹配，需要恢复其关联规则 |

最终核心309367项、共享导入59项、原生输入探针11项均0错误。共享报告运行目录`rewrite/reports/mod-import-20261019/d49ad70fddbc47e6803652b27ea9dab6`；核心报告`rewrite/reports/core_validation.json`。六包程序因仍有两个失败包正常返回非零，不能将退出码描述为全包兼容。

浏览器报告`rewrite/reports/mod-resolution-ui-20261019/results.json`：四个实际通过包、8个场景、16项检查/16张桌面及手机尺寸截图，画布非空、脚本异常0、缺失资源0。使用隔离5185，收尾已停止。不是完整通关或Android验收。

已发布Web运行时`rewrite/local/mod-resolution-runtime`及离线工具包`build/ew4-mod-kit`，工具包1962文件185357449字节。5184已切换PID38520，保存并恢复切换时立即当前的conquest1 revision0/252单位，九项状态一致、5个存档文件核验；证据`rewrite/reports/mod-import-20261019/preview-preservation.json`。此处状态仅为核验记录，禁止用来覆盖用户后续进度。前端app缓存20261019.16，未生成新APK。
