# Mod 切换与内容体验（问题域）

状态:**进行中**（分析稿，供拆任务）  
关联路线图：[roadmap.md](./roadmap.md)（M1–M8）

第二阶段已做到：**激活 Pack、parent 合并、zip/APK 工具导入、catalog 不 500**。下列是切换 Mod 后仍「粗糙」的主要原因（与代码位置对照，便于写 bug 单）。

---

## 1. 多套数据与路径

同时存在：

- `pack.json` / `mod.json`
- BTL 语义 + `scenarios/*.json`
- `original/def_*.xml`、`assets/tables/`、`data/catalog.json`
- `client/manifest.json`、`client/countries.json`
- `rewrite/*.png` → `/content/extensions/…`
- `client/*` → `/content/ui/…`（经 `PlayStaticFiles.MapContentPath`）

Mod 只改其中一层时，容易出现「逻辑是新表、UI 仍是 base manifest」。

**目标（M1）：** 作者只维护一份「包清单」文档 + 工具校验缺失项。

---

## 2. 地图涂色与势力

- 服务端：`ClientGame.View` 合并 `Appearance`、`CountryCatalog.Color`、BTL `factionColors`。
- 前端：`battlefield.js` 在 `color === 0` 时几乎不绘制陆地归属色。

动态势力或国家未进 `countries.json` 时易表现为「未知势力无涂色」。

**目标（M2）：** 缺省色策略 + 创建势力事件与 catalog 对齐。

---

## 3. 头像与国旗

- 国旗：`app.js` 已分 `flagImage`（extensions）与 `image_flag_hd`。
- 肖像：`menus.portrait` 仅查固定 commander 图集，**不用** HQ API 的 `definition.image`。

Mod 新将领常见占位图。

**目标（M3）：** 统一 `resolvePortrait(pack, definition)`。

---

## 4. 兵种卡片与数值展示

- 局内数值在 `/api/state`。
- 卡片与 `infomarker_*` 依赖 `manifest.profiles[profile]`。

Overlay 改兵种未更新 manifest 时，攻击/血量/图标错位。

**目标（M4）：** 构建期生成或校验 manifest；或 UI 回退到 API 提供的 profile 摘要。

---

## 5. 战役星级与账户

- 引擎：`BattleSettlement` + `PlayerAccount.CampaignStars`。
- 胜利界面：`victory-ui.js` 显示当局星级。
- 选关列表：**未**展示历史星级；`/api/catalog` **未**下发 `campaignStars`。
- 换包：`AccountContentRevision` 变化时账户部分字段重置。

**目标（M5）：** 产品规则定稿后接 API + UI。

---

## 6. 士气、音效、动画

| 能力 | 引擎/API | UI / 资源 |
|------|----------|-----------|
| 士气 | 有 | 几乎无条/图标 |
| 音效 | 时间轴触发 | `effects.json` 白名单 + `/content/sounds/` |
| 动画 | — | `movementStepMs`、战斗 timeline；体感偏慢可调参 |

---

## 7. 排查与记录

新缺陷用 [debug/TEMPLATE.md](../../debug/TEMPLATE.md)，流程见 [task/todo.md](../todo.md)。  
已结案范例：[debug/great-front-campaign-load.md](../debug/great-front-campaign-load.md)。
