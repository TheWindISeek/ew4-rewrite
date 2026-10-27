# 战役选关历史星级（B3 / M5）

| 字段 | 值 |
|------|-----|
| 日期 | 2026-10-27 |
| 状态 | 已验收 |
| 影响 | 战役菜单选关列表；当前存档槽 `PlayerAccount.CampaignStars` |
| 关联任务 | plan **B3**、**M5** |

## 行为

- 引擎：`BattleSettlement` 写入 `PlayerAccount.CampaignStars`（战役胜利、按 presentation 键）。
- API：`/api/play/catalog` 下发 `campaignStars`（与 `unlockedCampaigns` 同账户）。
- UI：`campaign-page.js` 在关卡名下方显示 1–5 颗 `star_middle.png`；回主菜单时 `refreshCatalog` 刷新。

## 验收

- [x] 战役胜利后账户有星级记录
- [x] 选关列表显示历史最佳星级
- [x] 结算屏星级与账户一致（原有 `victory-ui`）

## 相关代码

- `src/Ew4.Service/Play/PlaySession.cs` — `RouteCatalog`
- `src/Ew4.Front.Web/wwwroot/js/campaign-page.js`
- `src/Ew4.Front.Web/wwwroot/css/style.css` — `.campaign-stage-stars`
