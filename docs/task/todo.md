# 任务怎么推进

本文说明 **如何认领、完成、验收** 工作项。清单在 [plan/](./plan/)，已交付记录在 [done/](./done/)。

---

## 1. 目录约定

| 路径 | 用途 |
|------|------|
| **task/todo.md**（本文件） | 流程、门禁、纪律 |
| **task/plan/** | 未完成或进行中的需求（可多文件） |
| **task/done/** | 已结案任务，**按类别合并**，不堆零散阶段文 |
| **debug/** | 缺陷：现象与原因；格式见 [debug/TEMPLATE.md](../debug/TEMPLATE.md) |
| **arch/** | 代码结构与接口（稳定对照 `src/`） |
| **development/** | 环境、布局、规范等**稳定手册**（无任务清单） |
| **product/** | 维护者产品/策划记录 |

**冲突优先级：** `src/` > `arch/` > `task/plan/` > `product/PRD.md`。

---

## 2. 做一项 plan 任务的步骤

1. 在 [plan/roadmap.md](./plan/roadmap.md)（或子文档）确认 ID 与验收标准。
2. 实现前如需排查，可在 `debug/` 用模板起草；**修完后**补全并列入 [debug/README.md](../debug/README.md)。
3. 改代码后跑门禁（§3）。
4. 若行为/接口变了，更新对应 **arch/** 文档（不是 development，除非路径/命令变了）。
5. 完成后：把摘要写入 **task/done/** 对应类别（或扩写已有合并文），从 plan 勾选或删条目。

---

## 3. 回归门禁

| # | 命令 | 通过标准 |
|---|------|----------|
| G1 | `scripts/ew4.sh build -c Release` | 0 error / 0 warning |
| G2 | `scripts/ew4.sh test -c Release` | 全绿 |
| G3 | `scripts/ew4.sh validate smoke` | `mods/base` SmokeStart |
| G4 | （已移除）`check btl` | btl 往返由 `Ew4.Test.Platform.Mod` 覆盖 |
| G5 | `scripts/ew4.sh web` → 浏览器 | 开局、回合、存读档、切包 |
| G6 | （已移除）`check json-btl` | 同上 |

Mod HTTP 烟测：`scripts/dev/test-mod-switch.sh`（web 已起）。

环境命令详见 [development/environment.md](../development/environment.md)。

---

## 4. 纪律（Agent 与贡献者）

- `Ew4.Front.Web` **不**引用 `Ew4.Service` / `Ew4.Platform.*`
- 路径用 `Ew4.Platform.Persistence.Layout`（`InstallLayout`、`RepositoryPaths`）；维护 `mods/`，运行 `install/*/mods/`
- 不恢复应用内 APK、`Ew4.Mods`、Lua/API1；不引用 `backup/**/*.csproj`
- zip 走 HTTP；APK 走 `Ew4.Tool`；不加 shell「import mod」

---

## 5. 当前焦点

见 [plan/roadmap.md](./plan/roadmap.md) 文末「当前焦点」。
