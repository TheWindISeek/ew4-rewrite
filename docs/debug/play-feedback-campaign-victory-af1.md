# 战役胜利未结算（反法第一关）

| 字段 | 值 |
|------|-----|
| 日期 | 2026-10-27 |
| 状态 | 调查中 |
| 影响 | 战役模式；APK / WebView 真机反馈 |
| 关联任务 | plan **B11** |

## 现象

- 关卡：**反法同盟战役第一关**（具体 `scenarioId` 待复现时从存档/API 核对）。
- 已达成胜利条件后：**未弹出胜利/结算 UI**（`form_victory` / `victory-ui`）。
- 局内仍可 **继续移动部队**，像未结束一样。

## 可能原因（待验证）

| # | 假设 | 代码线索 |
|---|------|----------|
| A | 引擎 `Outcome` 仍为 `Continue` 或 `Phase` 未切 `Finished` | `WorldVictory.Resolve`、`ExtensionVictory`、`ScenarioRules` 事件 `victory` |
| B | 已 `Finished` 但 `battleResult` 未下发或为空 | `PlaySession` `BattleResultView`、`BattleSettlement` |
| C | 前端 `victory-ui.sync` 被 `api.ready()` 卡住，永不 `open` | `victory-ui.js`（非教程需连续 2 帧 ready） |
| D | 仍有 `dialogue` / `scenarioPopup` 阻塞结算 | `victory-ui.sync` 早退 |
| E | 战役扩展 `VICT` / 自定义胜利与占领目标不一致 | `ScenarioExtensionPostProcessor`、`ExtensionVictory` |

## 复现与采集（维护者）

1. 从胜利前一回合存档继续，或重开反法第一关打到胜利。
2. 浏览器 DevTools 或 `rpc('action',{op:'state'})` 看：`phase`、`outcome`、`battleResult`。
3. 若 `phase==='Finished'` 且无弹窗 → 偏 C/D；若仍为 `Active` → 偏 A/E。

## 验收

- [ ] 达成关卡胜利条件后不可再下达移动/攻击
- [ ] 结算屏弹出（星级/继续/退出与 Web 一致）
- [ ] 账户 `CampaignStars` 与选关列表更新（若适用）

## 相关代码

- `src/Ew4.Platform.Engine/World/WorldVictory.cs`
- `src/Ew4.Platform.Engine/Scenario/ScenarioRules.cs`
- `src/Ew4.Service/Play/PlaySession.cs`
- `src/Ew4.Front.Web/wwwroot/js/victory-ui.js`
