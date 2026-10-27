# 玩法 / UI  backlog（2026-10-27）

维护者反馈。条目分两类：

- **缺陷（bug）**：与原版或规则不一致，应优先修。
- **需求（feature）**：原版体验或新产品行为，需设计验收后再做。

状态：`待办` | `进行中` | `已验收`（结案时挪摘要到 [done/](../done/)）

---

## 缺陷（bug）

| ID | 状态 | 主题 | 说明 | 验收 |
|----|------|------|------|------|
| **B1** | 已验收 | 海洋行动力 | 陆地单位（运输）在海上每格应耗整段移动力；海军仍按 `SeaCost` | `MovementSeaTests`；局内运输船海上射程与原版一致 |
| **B2** | 已验收 | 普鲁士占地颜色 | 地图涂色与原版不一致（常伴随 BTL `color` 无 alpha 被前端丢弃） | `base` 下普鲁士与原版色值一致；`FactionAppearanceResolve` 与前端 `Ew4Appearance.factionColor` 一致 |

关联路线图 **M2**。

---

## 需求（feature）

| ID | 状态 | 主题 | 说明 | 验收 |
|----|------|------|------|------|
| **B3** | 已验收 | 战役通关星级 | 已通过战役显示星级；**不同档位不同结果**（非仅展示） | `catalog.campaignStars` + 选关星标；引擎 `CampaignStars` / 勋章增量；结算 `victory-ui` |
| **B4** | 已验收 | 回合跳过 → AI 快进 | 点「跳过 AI」即开启快进并持续自动跑 AI | 一次点击进入 `skipTurnFast` + `skipAi`；可关；设置可记盘 |
| **B5** | 已验收 | 新回合数值信息 | 进关 `form_stageintro`；每回合 `form_roundturn`（经济/回合/补给 + 上阵将领槽） | 原版布局 `turn-briefing-ui.js`；`battleBriefing` + `turnSummary` |
| **B6** | 已验收 | Mod 独立界面 | Mod 入口、内容包、创作均为菜单全屏页（同指挥部） | `#mod-screen` / `#mod-packages-screen` / `#editor-screen` |
| **B7** | 已验收 | 兵种移动动画 | 路径逐步动画，非瞬移 | 玩家 `move` 响应带 `movements`；`app.js` `installState` 步进动画；与 `settings.speed` 协调 |
| **B8** | 已验收 | 教程与进局 UI | 教程场景 JSON、LOADING 与说明/回合弹窗、等待步任意点击继续、结算/回主菜单 | `tutorial.js`、`app.js`、`victory-ui.js`；base 需 `tutorials*.json`（见 debug） |
| **B9** | 已验收 | 存档槽确认钮 | 可操作时用彩色 OK，禁用仍灰色 | `saves-page.js` |
| **B10** | 已验收 | 进关胜利条件区 | `form_stageintro` 标题与「回合」文案，弱化误选感 | `turn-briefing-ui.js`、`original-ui.css` |

**B7** 属 **M8** 子项。

---

## 实现索引

| ID | 可能涉及 |
|----|----------|
| B1 | `HexMap.Movement`、`WorldActions.MovementContext` |
| B2 | `FactionAppearanceResolve`、`appearance.js`、`battlefield.js` |
| B3 | `CampaignStars`、`BattleSettlement`、`campaign-page.js`、catalog |
| B4–B5 | `app.js`、`original-ui.js`、回合 `notices` |
| B6 | `mods.js`、`html/index.html` 或独立页 |
| B7 | `PlaySession.View`、`app.js` `installState` |
| B8–B10 | `tutorial.js`、`app.js`、`saves-page.js`、`turn-briefing-ui.js` |

---

验收记录见 [debug/play-feedback-b2-b4-b7.md](../debug/play-feedback-b2-b4-b7.md)、[debug/play-feedback-tutorial-menu-ui.md](../debug/play-feedback-tutorial-menu-ui.md)、[debug/play-feedback-move-territory.md](../debug/play-feedback-move-territory.md)、[debug/play-feedback-b3-campaign-stars.md](../debug/play-feedback-b3-campaign-stars.md)、[debug/play-feedback-m6-morale-ui.md](../debug/play-feedback-m6-morale-ui.md)。**B1–B10** 已结案。

---

## 缺陷（续，2026-10-27 APK 试玩）

| ID | 状态 | 主题 | 说明 | 验收 |
|----|------|------|------|------|
| **B11** | 待办 | 战役胜利未结算 | 反法第一关：已胜仍无胜利 UI，可继续移动 | `phase`/`outcome`/`battleResult` 一致；`victory-ui` 弹出且禁止操作 |

见 [debug/play-feedback-campaign-victory-af1.md](../debug/play-feedback-campaign-victory-af1.md)。

---

## 需求（续，APK / 手机）

| ID | 状态 | 主题 | 说明 | 验收 |
|----|------|------|------|------|
| **B12** | 待办 | 局内角落按钮触控 | 结束回合、跳过 AI 等难点 | 真机稳定命中；可加大热区或壳内默认缩放 |
| **B13** | 待办 | 载入时画面比例 | **每次载入**前台选档：100% / 50% / 75% 等比；后台照常启动 | 三档可选、记住上次、与 `settings.screen` 或新预设一致 |

见 [debug/play-feedback-apk-mobile-ui.md](../debug/play-feedback-apk-mobile-ui.md)。关联路线图 **M9**（壳内体验）。
