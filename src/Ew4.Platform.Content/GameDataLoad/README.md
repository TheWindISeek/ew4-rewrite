# GameDataLoad

**游戏数据载入**：在 Mod 快照之上，把封存路径（`original/*`、`data/*`）中的 XML/BTL/ini 解析为引擎可用的 catalog、地图与规则表。

当前实现仍位于同程序集的 **`Build/`**、**`Original/`** 与 **`ScenarioEdit/`**（codec）；本目录仅作架构命名锚点，见 [docs/arch/mod-layers.md](../../../docs/arch/mod-layers.md)。

| 区域 | 入口 |
|------|------|
| 路径映射 | `Build/ModAssetRead.cs`、`ModAssetReader` |
| 运行时引导 | `Build/ModRuntimeBootstrap.cs`、`LegacyPackRuntimeLoader.cs` |
| 将领表 overlay | `Build/LegacyCatalogCommanderOverlay.cs` |
| 封存表 | `Original/*` |
| 剧本 codec | `ScenarioEdit/*` |
