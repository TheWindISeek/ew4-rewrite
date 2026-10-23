# Ew4.Platform.Content

Pack 读取、迁移、校验、导入导出与 Builder。见 [docs/architecture/03-content-and-packs.md](../../docs/architecture/03-content-and-packs.md)。

## 已实现（P1 骨架）

| 类型 | 说明 |
|------|------|
| `DirectoryPackSource` / `ZipPackSource` | 读取目录与 zip |
| `MergedPackSource` | 按 parent 整文件补全 |
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
