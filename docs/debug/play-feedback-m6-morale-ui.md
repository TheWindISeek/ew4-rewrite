# 局内士气图标（M6）

状态：**已验收**（2026-10-27）  
关联：[roadmap.md M6](../task/plan/roadmap.md)、[mod-experience.md §6](../task/plan/mod-experience.md)

## 范围

- 引擎/API 已有 `unit.morale`（`PlaySession` 视图字段 `morale`）。
- 本项只做**局内地图**可视化，不新增 i18n 文案。

## 行为

| `morale` | 精灵（`buildings` / `buildings_hd`） |
|----------|--------------------------------------|
| `> 0` | `morale_up.png` |
| `-1` | `morale_down1.png` |
| `-2` | `morale_down2.png` |
| `≤ -3` | `morale_down3.png` |
| `0` | 不显示 |

- 绘制在将领立绘**右侧**（`battlefield.js`），避免挡住 `commandermark`。
- 移动后士气变化依赖引擎 `WorldActions.Move` 内 `RefreshMorale`（与包围/补给规则一致）。

## 实现索引

- `src/Ew4.Front.Web/wwwroot/js/battlefield.js` — `moraleSprite`、单位绘制
- `tests/Ew4.Test.Front/battlefield-hex.test.js` — `moraleSprite` 映射

## 验收

- [x] 非零士气单位在战场上可见对应图标
- [x] 士气为 0 不显示
- [x] 有将领时图标不遮挡头像
