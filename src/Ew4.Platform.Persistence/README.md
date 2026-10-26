# Ew4.Platform.Persistence

**唯一磁盘边界**：install/user 路径、mod 包读写、槽位序列化、`active.json`、创作 workspace 与导入 staging 的低级 IO。  
Mod / Content / Editor **业务层**不直接 `File.*`；经本程序集 API（`Io`、`Workspaces`、`Packages` 等）。

见 [docs/arch/module-responsibilities.md](../../docs/arch/module-responsibilities.md)。

| 目录 | 职责 |
|------|------|
| `Layout/` | `InstallLayout`、`InstallChannelProbe`、`RepositoryPaths`、`SessionPaths` |
| `Io/` | `PhysicalFiles`、`LoosePathStore` |
| `Workspaces/` | `WorkspaceStore`（创作区 overlay 写盘） |
| `Import/` | `ModImportStagingStore`、`TemporaryScratchStore` |
| `User/` | `active.json` |
| `Packages/` | 已安装 mod 目录/zip、包内文件、`FileModRepository` |
| `Serialization/` | `WorldSave`、`SessionSave`、`SlotSaveEnvelope`、`PlaySlotPersistence`、`PlayerAccountPersistence`、`EngineSlotPersistence` |
| 根 | `SaveDirectoryLayout`、`SaveSlotId` |
