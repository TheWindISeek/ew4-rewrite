# Ew4.Platform.Mod

**Mod 领域**（非 HTTP）：激活编排接口、面板 catalog、导入 stage/commit、parent 合并与快照索引。引用 **`Ew4.Platform.Persistence`**；**不**直接扫 install 目录（经 `FileModRepository` 等 Persistence API）。

| 目录 | 职责 |
|------|------|
| `Activation/` | `IModHost` — 由 `HostApp` 实现（激活、删除、从归档安装） |
| `Catalog/` | `ModCatalogService`、`ModPanelApi` — 已安装 mod 面板状态 |
| `Import/` | `ModImportService` — 上传暂存与 commit 编排（写盘经 Persistence） |
| `Merge/` | `ModMerge`、`MergedModSource` |
| `Snapshot/` | `ModSnapshotIndex`（契约 `IModSnapshot` / `ModContentRef` 在 **Interface.Mod**） |

`ModSnapshotLoader` 与 `ResolvedContent` 组装在 **`Ew4.Platform.Content/Snapshot`**（依赖 Content 校验与解析）。

Web 入口：`Ew4.Service` 的 `ModOps` 实现 **`IModOperations`**，经 `IGameOperations.Invoke` 分派。
