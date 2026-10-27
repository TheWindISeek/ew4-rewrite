# 玩法反馈：教程、存档、进关说明与 Mod 全屏

| 字段 | 值 |
|------|-----|
| 日期 | 2026-10-27 |
| 状态 | 已结案（维护者反馈「凑合能用」） |
| 影响 | 教程开局/等待步；存档槽确认钮；`form_stageintro`；LOADING；Mod/创作全屏；`ew4.sh web` |
| 关联任务 | [plan/play-feedback-2026-10.md](../task/plan/play-feedback-2026-10.md) B6、B8 |

## 现象

- **教程**：基础/高级教程点选后进不去或一直 LOADING；教程中要求点指定格/按钮，点对话框或附近格子无反应；打完或走完脚本不退出主菜单。
- **进关说明**：`form_stageintro` 底部两格只有数字与「回合」，缺少「胜利 / 重大胜利」标题，线框像可选框。
- **存档**：槽位可操作时确认钮仍为灰色图。
- **Mod**：内容包/创作希望与 Mod 入口同为菜单全屏（B6）。
- **Web 开发**：`ew4.sh web` 需 start/restart、就绪等待与 logs 端口提示。

## 原因（摘要）

| 项 | 说明 |
|----|------|
| 教程开局 | `tutorials1` / `tutorials2` 仅有 BTL，catalog 无 `scenarios/tutorials*.json` 时 `start` 报 `Sequence contains no matching element` |
| LOADING | `withLoading` 内 `installState` 会 `await` 关卡说明/回合面板，LOADING 层 `z-index:20` 盖住 `detail-modal`，无法点关闭 |
| 教程等待 | 原版脚本 `wait area` / `wait ui` 要求精确格子或控件；Web 坐标/选中与脚本 id 易错位 |
| 教程退出 | `showMain` 在 `leave` RPC 失败时不切回菜单；教程局胜利结算 `ready()` 过严 |
| 进关 UI | 未写 `text_round` 与 groupbox 标题键（`text_victory` / `text_bestvic`） |
| 存档 UI | 槽位确认钮固定 `button_ok_gray_noshadow.png` |

## 修复

| 改动 | 文件 / 内容 |
|------|-------------|
| 教程场景 JSON | 本地 `mods/base/scenarios/tutorials1.json`、`tutorials2.json`（`Ew4.Tool scenario btl-to-json`）；`sync-mods web` 后重启 Web |
| LOADING 与弹窗 | `app.js`：展示 `battleBriefing` / `turnSummary` 或事件对白前关闭 LOADING |
| 教程等待 | `tutorial.js` 放宽 `accepts`；`app.js` `tutorialDismissWait`：战场内任意点击推进 `wait touch` / `wait area` / `wait ui` |
| 教程结算/退出 | `victory-ui.js` 教程局直接开结算；`showMain` 失败仍回主菜单 |
| 进关说明 | `turn-briefing-ui.js`、`original-ui.css`（`form_stageintro`） |
| 存档钮 | `saves-page.js` 可用时用 `button_ok_noshadow.png` |
| Mod 全屏 | `#mod-screen`、`#mod-packages-screen`、`#editor-screen`（`mods.js`、`index.html`、`mods.css`） |
| Web | `scripts/dev/web.sh` 状态文件、就绪等待、`web logs` |
| 文案 | `scripts/dev/lang/ui.json`：`ui.tutorial_tap_map` 等 |

## 内容包维护（base，不进 Git）

```bash
# 若本地 base 尚无教程 JSON：
scripts/ew4.sh shell dotnet run --project src/Ew4.Tool -- scenario btl-to-json \
  --id tutorials1 --input mods/base/assets/original/tutorials1.btl --output mods/base/scenarios/tutorials1.json
scripts/ew4.sh shell dotnet run --project src/Ew4.Tool -- scenario btl-to-json \
  --id tutorials2 --input mods/base/assets/original/tutorials2.btl --output mods/base/scenarios/tutorials2.json
scripts/ew4.sh sync-mods web
scripts/ew4.sh web restart
```

## 验证

- 主菜单 → 教程 → 基础/高级：能进局，LOADING 后可关进关说明与回合面板。
- 教程进行中：任意点击可过等待步（`wait action` 仍须真实操作）。
- 战役进关：说明窗显示胜利条件标题与「回合」。
- 存档/读档：有档时确认钮为彩色 OK。
- Mod / 内容包 / 创作：全屏页可进可退。
