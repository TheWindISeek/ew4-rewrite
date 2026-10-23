# Ew4.Platform.Content

Mod 包读取、**Mod 运行时**快照、**GameDataLoad**（表/剧本载入）、校验与导出。见 [docs/arch/mod-layers.md](../../docs/arch/mod-layers.md)、[GameDataLoad/README.md](./GameDataLoad/README.md)。

## 已实现（P1 骨架）

| 类型 | 说明 |
|------|------|
| `DirectoryPackSource` / `ZipPackSource` | 读取目录与 zip |
| `PackMerge` / `MergedPackSource` | 按 parent 整文件补全（统一入口） |
| `PackScenarioCatalog` | Play 剧本 id 与 overlay 显示名 |
| `LegacyPackRuntimeLoader` | 共享 catalog / 地图加载（Play 与剧本构建） |
| `PackArchiveProbe` | 导入前校验 zip 含 `mod.json` |
| `ContentResolver` | 引擎要求、指纹、`ResolvedContent` |
| `ContentValidator` | Schema 级校验与路径白名单 |
| `FilePackRepository` | `packs/{id}/` 安装与打开 |
| `ZipPackExporter` | 导出 `.ew4mod` |
| `ScenarioJsonBuilder` | 剧本 JSON + BTL → `BuiltScenario` |
| `Original/*` | 封存 BTL / 规则表导入（`Ew4.Platform.Content.Original`，原 `Build/Legacy*`） |
| `Build/PackRuntimeBootstrap` | 从 Pack `assets/` 加载 catalog / 地图 |
| `Build/PackAssetRead` | `data/*`→`assets/tables/*`（兼容 `assets/legacy/*`），`rewrite/*`→包根或 `assets/rewrite/*` |
| `Build/ScenarioExtensionPostProcessor` | Mod Victory / 剧本事件 / 扩展 Bus（对齐旧 Client） |
| `BtlReader` | BTL 头摘要（`Importers/Original`） |

包导入、APK 转包、剧本嵌入与离线校验见 **`Ew4.Tool.Import`**，不在本程序集。
