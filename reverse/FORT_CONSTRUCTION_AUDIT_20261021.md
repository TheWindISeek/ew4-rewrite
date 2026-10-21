# 要塞建造、拆除及施工外观核查

日期：2026-10-21。初轮诊断之后，已按用户追问修复施工外观，下面保留原核查依据。

## 施工外观修复

- `ClientGame.View` 为单位透传实际 `constructionTurns`。战场在值大于零时选择既有 `build` 动作，沿用当前兵种的 `def_motion` / `profile.motions` 映射；原包四类要塞均映射到 `army_fort` 的 `Building` 脚手架。归零后自动恢复各自正常 `ready` 外观，两方向及缓存切换均有效；缺少施工字段的旧前端状态按正常外观兼容。
- 不另加计时器、固定兵种编号或额外素材；实际施工2/3/4/3回合、攻击/反击限制及拆除规则保持。Mod继续使用自己提供的 `build` 映射。`battlefield.js` 缓存版本更新至 `20261021.16`。
- 新增 `FortConstructionChecks`，接入 `--client-only`：真实招募/完整己方回合推进/存档加载共88项，四种要塞16个阶段，验证施工计数、投影、攻击和反击限制。完整客户端2289项、错误0。
- `tools/verify_fort_construction.cjs` 浏览器63项、10张截图，异常/缺图0；覆盖16阶段、左右朝向、原贴图非空像素、施工/完工缓存切换、缺字段兼容及手机横竖屏。实际战场截图已目视确认脚手架与完工差别。报告和合成存档在 `rewrite/reports/fort-construction-20261021/`，测试资源数是夹具数据。
- Web构建及专用.NET工具链的Android ARM64 Compile均0警告/0错误，JS语法及相关diff检查通过。独立预览 <http://127.0.0.1:5241>，PID39008，程序集和测试档案在 `build/fort-construction-20261021/`。原计划5227被其他服务占用，本轮未操作该服务；无新APK、发布或真机运行验收，玩家档案未动。

## 初轮诊断结论

- 建造后拆除返还资源，但不恢复本回合建造次数。`LegacyRecruitmentCatalog.Offers` 将四类要塞绑定到同一 `ew4.fortress` 组，每势力每回合限一次。`WorldDisband.Execute` 只移除单位并返还资源，不修改 `TurnUses`；`WorldTurns.StartFaction` 在下个己方回合清空次数。原版资格/提交/刷新链已验证每回合一次限制，参见 `FACTION_RECRUITMENT.md`。本轮没有另行执行原版拆除链，不将拆除返还次数的原版行为冒称为直接验证结果。
- 后端没有立即完成施工。原卡37/38/39/40的 `buildround` 分别为2/3/4/3；招募写入 `Combatant.ConstructionTurns`，己方回合开始减一，施工时禁止主动攻击和要塞反击。
- 确认前端遗漏施工外观：`ClientGame.View` 没有输出施工回合，`battlefield.js` 总是选择普通 `ready` 外观，没有施工分支。原包存在脚手架 `army_fort.xml` 的 `fortress_lv0.png`（x148/y111/w180/h109），已导入 `data/client/images/army_fort.png`。`data/client/animations/army_fort.json` 的 `Building` 项引用 `fortress_lv0`。用户明确指出原版有施工中外观；本轮已核实贴图和动画引用，没有重新执行原版绘制调用链。

## 隔离验证

夹具：`build/fort-audit-20261021/FortAudit.csproj`；证据：同目录 `result.json`。使用真实原版目录、征服1法国开局、实际招募/解散/完整势力轮转接口。测试账户独立；为覆盖四种要塞，夹具给予足额资源并补齐对应兵种授权，不更改原文件。

66项，错误0。四类要塞均确认：扣费/返还、共享次数限制、拆除不恢复次数、下回合可在另一合法地块重建、施工计数每己方回合减一、施工中禁止攻击及反击、完工后攻击资格恢复。实际客户端投影确认未提供 `constructionTurns`。

| 卡片 | 初始施工回合 | 实测计数 |
| --- | --- | --- |
| 37 小型要塞 | 2 | 2 → 1 → 0 |
| 38 要塞 | 3 | 3 → 2 → 1 → 0 |
| 39 大型要塞 | 4 | 4 → 3 → 2 → 1 → 0 |
| 40 海岸炮 | 3 | 3 → 2 → 1 → 0 |

上述施工外观修复现已完成。原版剩余回合数字显示的位置/样式尚未核定，本次只恢复已有施工外观；保留施工规则和每回合次数限制。
