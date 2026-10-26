# 路线图（第三阶段起）

状态:**进行中**  
最后更新:2026-10-23  
基线:第二阶段已结案（[done/refactor.md](../done/refactor.md)）。

本文是 **产品/工程 backlog**，不是设计终稿。条目进入实现前应：在 `roadmap.md` 标为进行中、必要时拆 `docs/debug/<topic>.md` 写清验收，代码合并后写入 [done/](../done/) 并更新 plan。

---

## P0 — Mod 可玩性（切换后的「像一款游戏」）

| ID | 主题 | 验收方向（摘要） |
|----|------|------------------|
| M0 | **统一 mod.json 清单** | 单文件含 `scenarios` 扩展；见 [pack-manifest-unify.md](./pack-manifest-unify.md) |
| M1 | **Mod 内容契约收敛** | 合并后清单 + `countries` / `manifest` / `unit-visuals` / `effects` 最低集；导入工具产出同一形状（[mods/README.md](../../mods/README.md)） |
| M2 | **势力外观统一** | 地图涂色不为 0；未知势力有默认色；`flag` / `flagImage` / `Appearance` 一条解析链（前后端一致） |
| M3 | **肖像与扩展图** | 指挥部/战役/对话/结算共用资源解析（atlas + `rewrite/` + API 下发的 `image`） |
| M4 | **兵种 UI 与 Mod 表一致** | `manifest.profiles` 与 overlay 的 `def_army` 同步策略；信息面板攻击/血量/图标不错位 |
| M5 | **战役进度展示** | `CampaignStars` 进 catalog 或专用 API；选关界面显示历史星级；明确按包隔离存档规则 |
| M6 | **士气表现** | 规则已有；补原版级 UI（条/图标/晕厥） |
| M7 | **音效** | Mod 可声明音效；`effects.json` 合并/回退策略可测 |
| M8 | **动画与性能** | 先调参（`settings.speed`、步长）；性能 profiling 单列，不阻塞 M1–M7 |

问题域拆解：[mod-experience.md](./mod-experience.md)。  
已修复案例索引：[debug/README.md](../debug/README.md)。

---

## P1 — 工程与质量

| ID | 主题 | 验收方向（摘要） |
|----|------|------------------|
| E1 | **测试分层** | 新增 `Ew4.Test.Service`、`Ew4.Test.Platform.Play` 等（见 [tests/README.md](../../tests/README.md)） |
| E2 | **Mod 回归** | 扩展 `test-mod-switch.sh`：除 catalog 计数外，可选快照/关键 API 字段（势力 color、scenario 数） |
| E3 | **Legacy 命名收敛** | `Client*` → 域名词（非阻塞）；`Original*` 边界文档化，避免新代码再扩 legacy 路径 |
| E4 | **文档** | `arch/` 与实现对账；根 README 保持与实现一致 |

---

## P2 — 平台（暂缓）

| ID | 主题 | 说明 |
|----|------|------|
| H1 | **Android 壳** | 路线 A：APK 内带后端 + WebView；见 [android-shell.md](./android-shell.md) |
| L1 | **多语言** | `zh-Hans/zh-Hant/en`；见 [arch/localization.md](../../arch/localization.md) |
| H2 | **发行内置 Mod** | 默认 `base` + 样例包打进 `install/mods/` 的安装器体验 |
| H3 | **APK 导出** | 内置 `ew4-base.apk` + 合并 Mod → 可安装 APK；`Ew4.Tool export-apk`；见 [apk-export.md](./apk-export.md) |

---

## 当前焦点（维护者填写）

- **进行中：** Mod 体验（M3–M8，见 [mod-experience.md](./mod-experience.md)）
- **已在本分支：** M0（`mod.json`）、M1（客户端资源契约告警）、M2（势力外观/涂色/旗）
- **下一步建议：** M3 肖像统一，M4 兵种 UI

流程与门禁见 [../todo.md](../todo.md)；本文件记 **功能 backlog**。
