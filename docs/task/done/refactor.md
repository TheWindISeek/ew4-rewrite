# 重构（已结案）

分支 `refactor/pack-architecture`。下文合并原 migration / phase2 / baseline 文档。

---

## 阶段一：新栈 MVP（P0–P4）

| 里程碑 | 交付 |
|--------|------|
| P0 | 契约项目、架构文档 |
| P1 | Engine 门面、Pack 读取、BTL/XML 往返 |
| P2 | 内容解析、SmokeStart、Builder |
| P3 | Session、游玩 API、Web 宿主、Mod 切换 |
| P4 | 创作 MVP、编辑器 API |

旧代码封存标签 `legacy/0.2.12-sealed`；新代码仅在 `src/`。

---

## 阶段二：做减法、安装布局、包规范（S0–S7）

### 已确认决策（摘要）

| 决策 | 内容 |
|------|------|
| D1 | 应用内不再 APK 导入；删 `Ew4.Mods` |
| D2 | 剧本以 JSON 为主；运行时内部生成 BTL |
| D3 | `parent` 单层继承 |
| D4–D11 | Engine 命名、Android 暂停、`Ew4.Interface`、`Ew4.Service`、Front/Backend 分离、删 Lua/API1 等 |

### 执行步骤（均已完成）

| 步骤 | 内容 |
|------|------|
| S0 | 基线 G1–G5 记录 |
| S1 | 切断 APK |
| S2 | 删旧栈与 Lua/API1 |
| S3 | `install/` + `EW4_HOME` |
| S4 | `Ew4.Interface`、PlayApi→Service 等改名 |
| R5 | Platform 命名、`Original*`、`assets/tables` |
| S5 | 剧本 JSON、G6 json-btl |
| S6 | `pack.json`、`PackInstallGuard` P1–P8 |
| S7 | 文档与实现对齐 |

现行结构见 [arch/module-responsibilities.md](../../arch/module-responsibilities.md)。

---

## S0 基线（2026-10-23，提交 `36fc30b`）

| 门禁 | 结果 |
|------|------|
| G1 build | 通过，0 警告 |
| G2 test | 9 通过 / 0 失败 |
| G3 smoke | 通过（warnings=6 为多人剧本无包内 BTL） |
| G4 btl | 95 个 BTL，failures=0 |
| G5 web | 当时未自动执行；后续以人工烟测为准 |

后续改动应重新跑 G1–G6，并与上表对比。
