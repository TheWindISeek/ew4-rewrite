# 玩法反馈首轮：涂色 alpha、玩家移动动画、AI 跳过快进

| 字段 | 值 |
|------|-----|
| 日期 | 2026-10-27 |
| 状态 | 已结案（B1/B2/B4/B5/B7 维护者已目视；B6/B8–B10 见 [play-feedback-tutorial-menu-ui.md](play-feedback-tutorial-menu-ui.md)；B3 仍待办见 plan） |
| 影响 | 涂色；海上移动力；回合 UI；玩家移动动画；AI 快进 |
| 关联任务 | [plan/play-feedback-2026-10.md](../task/plan/play-feedback-2026-10.md) B1–B7（除 B3/B6） |

## 现象

- **B2**：普鲁士等势力占地颜色与原版不一致，或呈占位灰。
- **B7**：玩家移动瞬移；AI 移动有路径动画。
- **B4**：点「跳过 AI」应进入快进自动跑敌方回合。

## 原因

| 项 | 说明 |
|----|------|
| B2a | 剧本/BTL 色为 **`0xAARRGGBB`**，前端 `map-surface.rgba` 曾按 `0xAABBGGRR` 解包，法国等偏色。 |
| B2b | 普鲁士 **`0x28000000`** = A=40、RGB=0，原版为半透明暗色叠地；误当「未指定」走 `DefaultPackedColor` 会变成亮绿。 |
| B7 | 玩家 `move` 只 `return View()`，响应无 `movements`；前端 `installState` 依赖 `next.movements` 做 `playMovements` / `motions`（AI 走 `combat.movements` 一直有动画）。 |
| B4 | 默认未开 `skipTurnFast`；`#skip-ai` 未同步勾选。 |
| B4b | `fastSkipping()` 误要求 `skipAi`，而 `runAi()` 开头把 `skipAi` 置 `false`，仅勾选设定不进 `ai_batch`。 |

## 修复

| 改动 | 文件 |
|------|------|
| `PackBattleColor`（AARRGGBB + RGB=0→回退） | `FactionAppearanceResolve.cs` |
| `rgba` / `factionColor` 按 AARRGGBB | `map-surface.js`、`appearance.js` |
| 玩家 `move` 下发 `movements` | `PlaySession.cs` |
| 默认快进 + 跳过按钮 | `app.js` |
| `fastSkipping` 只看 `settings.skipTurnFast`；`runAi` 开局同步 `skipAi` | `app.js` |

## 验证

```bash
scripts/ew4.sh build -c Release
scripts/ew4.sh web   # http://127.0.0.1:5184
```

### 2026-10-27 自动化 / 半自动

| 项 | 结果 |
|----|------|
| 服务 | `curl http://127.0.0.1:5184/` → 200；主菜单可开 |
| B4 | 设定 → **「跳过回合快进」默认勾选**（浏览器 snapshot `e57` checked） |
| B2 API | 普鲁士应保持 `color === 0x28000000`（671088640）；前端 `rgba(0,0,0,40)` 叠地 |
| B2 涂色 | 维护者目视 `campaign1_06` 等地涂色正常 |
| B7 | **`PlaySession` `move` → `View(worldBeforeMove, playerMovements)`**；维护者确认玩家单位路径步进与 AI 一致 |
| 单测 | `FactionAppearanceResolveTests`、`MovementSeaTests` 通过 |

- [x] 设定里默认「跳过回合快进」
- [x] API 普鲁士势力色非零黑
- [x] 地图涂色与原版截图对比（维护者，2026-10-27）
- [x] 局内玩家移动动画（维护者，`Active` 阶段，2026-10-27）
- [x] **B1** 运输船海上每格整段移动力（`MovementSeaTests`）
- [x] **B5** 进关 `form_stageintro`、玩家回合 `form_roundturn`（`turn-briefing-ui.js`，2026-10-27）

## B5 / B1（补充）

| 项 | 说明 |
|----|------|
| B5 进关说明 | `battleBriefing` 第 1 回合进局即下发（不绑 `faction.started`）；前端 `shownStageIntro` 防重复 |
| B5 回合面板 | `turnSummary` + `DeployedGeneralsView`；布局 `form_roundturn` |
| B1 | `HexMap.EntryCost`：非海军 `Transport` 进海格用 `FullBudget` |

## 相关代码

- `src/Ew4.Platform.Content/Start/FactionAppearanceResolve.cs`
- `src/Ew4.Service/Play/PlaySession.cs`
- `src/Ew4.Front.Web/wwwroot/js/{app,appearance,map-surface,turn-briefing-ui}.js`
- `src/Ew4.Platform.Engine/World/HexMap.cs`
- `scripts/dev/web.sh`（`ew4.sh web start|restart`）
