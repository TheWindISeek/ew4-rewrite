# GameDataLoad

**游戏数据载入**：在 Mod 快照之上，把封存路径（`original/*`、`data/*`）中的 XML/BTL/ini 解析为引擎可用的 catalog、地图与规则表。

实现位于同程序集的 **`CatalogLoad/`**、**`StaticData/`**、**`Campaign/`**（含 `Importers/`）与 **`Snapshot/`**。本目录为架构命名锚点。见 [module-responsibilities.md](../../../docs/arch/module-responsibilities.md)。

| 区域 | 入口 |
|------|------|
| 路径映射 | `CatalogLoad/ModAssetRead.cs`、`ModAssetReader` |
| 运行时引导 | `CatalogLoad/ModRuntimeBootstrap.cs`、`LegacyModRuntimeLoader.cs` |
| 将领表 overlay | `CatalogLoad/LegacyCatalogCommanderOverlay.cs` |
| 封存表 | `StaticData/*` |
| 剧本 codec | `Campaign/*` |
