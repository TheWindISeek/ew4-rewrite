# APK 局内触控与载入画面比例

| 字段 | 值 |
|------|-----|
| 日期 | 2026-10-27 |
| 状态 | 待办 |
| 影响 | Android WebView 壳；小屏手机 |
| 关联任务 | plan **B12**、**B13** |

## 现象（维护者反馈）

1. **角落控件难点**：结束回合、跳过 AI 等 `corner-button`（约 28×28px × `--battle-scale`）在手机上 **触控命中差**，易误触地图或点不到。
2. **期望：载入时选画面比例**：后端解压/启动仍在后台进行时，前台 **每次进局/载入** 都弹出比例选择（不只在设置里改一次）：
   - **100%**：与当前逻辑一致（`settings.screen` 默认 100，按视口 fit 568×320 基准）。
   - **50%**：长宽等比缩至约 **1/2**（整体 UI + 战场更「缩小」、留白或更大可操作区）。
   - **75%**：约 **3/4** 等比，介于全屏与半屏之间。

选择结果写入玩家设置（`user` 渠道），下次载入仍可改（**每次载入都询问**，便于换设备试手感）。

## 实现方向（草案）

| 项 | 说明 |
|----|------|
| B12 触控 | 增大角落按钮 hit area（`min-touch` padding）、或 APK 下提高 `--battle-scale` 下限；跳过 AI 在 AI 跑时更显眼 |
| B13 载入比例 | `#loading-screen` 增加预设三档；与现有 `settings.screen` 或新键 `displayPreset` 映射；选完再 `showMain` / 进 `play` |

## 验收

- [ ] 真机可稳定点结束回合、跳过 AI
- [ ] 每次从主菜单进战斗/读档路径出现比例选择（可默认高亮上次选项）
- [ ] 三档比例下地图与 HUD 不变形，仅整体缩放

## 相关代码

- `src/Ew4.Front.Web/wwwroot/css/style.css` — `.corner-button`、`--battle-scale`
- `src/Ew4.Front.Web/wwwroot/js/app.js` — `settings.screen`、loading / `installState`
- `src/Ew4.Front.Web/wwwroot/html/index.html` — `#loading-screen`、`#skip-ai`、`#end-turn`
