# Ew4.Platform.Content

在 **Mod 合并视图**（`Ew4.Platform.Mod`）之上做 **CatalogLoad / StaticData / Campaign** 与校验、开局。读包与写盘经 **`Ew4.Platform.Persistence`**。见 [docs/arch/module-responsibilities.md](../../docs/arch/module-responsibilities.md)、[GameDataLoad/README.md](./GameDataLoad/README.md)。

## 目录

| 路径 | 职责 |
|------|------|
| `CatalogLoad/` | 表与扩展载入、`ModRuntimeBootstrap`、`LegacyModRuntimeLoader` |
| `StaticData/` | 封存 XML/BTL 域模型 |
| `Campaign/` | 战役 JSON/BTL codec；`Importers/` |
| 根目录 | `ModSnapshotLoader`、`ContentResolver`、`ScenarioJsonBuilder` 等 |

## 主要类型

| 类型 | 说明 |
|------|------|
| `ContentResolver` | 引擎要求、指纹、`ResolvedContent`（实现 `IModSnapshot`） |
| `ContentValidator` | Schema 级校验 |
| `ModSnapshotLoader` | 载入期合并视图 + 内容指针索引 |
| `ModScenarioCatalog` | 剧本 id 与 overlay 显示名 |
| `ScenarioJsonBuilder` | 剧本 JSON + BTL → 开局 |

parent 整文件补全：`Ew4.Platform.Mod.Merge`（`ModMerge`、`MergedModSource`）。

离线造包与导入见 **`Ew4.Tool`**。
