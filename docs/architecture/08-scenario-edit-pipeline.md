# 08 剧本编辑与 BTL 编解码（一体管线）

状态:草案（实现中）  
依赖:[03 内容包 §6](03-content-and-packs.md#6-格式转换importers-与-exporters)、[05 创作层](05-authoring.md)

## 1. 原则

| 规则 | 说明 |
|------|------|
| **单一真相** | 作者面对 `scenarios/{id}.json`（完整 `ScenarioDocument`，schema 2）。 |
| **同一条 Codec** | 导入、导出、编辑器保存、开局、`Ew4.Tool` 往返**只调用** `ScenarioBtlCodec`。 |
| **未知保真** | 已理解的层为 typed 字段；其余 section / 扩展尾以命名二进制块保留。 |
| **BTL 是内部 IR** | 开局统一经 `ScenarioBtlResolver` 得到 BTL 字节（`OriginalBattle`）。来源抽象为：嵌入 JSON → `ScenarioBtlCodec`，或 legacy 包内 `.btl`。 |
| **主文件 `mod.json`（包根）** | 声明 `scenarios.storage: embedded-json` 时**先走新格式**，失败再回退 legacy BTL；`legacy-btl` 显式优先 BTL；无文件则按每个剧本 JSON 自动判断。 |
| **转换工具** | `Ew4.Tool pack-convert-legacy-scenarios`：读 legacy BTL → 写入嵌入 `scenarios/{id}.json` 并生成 `mod.json`（**不删**包内 BTL，除非显式 `--remove-btl`）。 |

## 2. 数据模型

```text
ScenarioDocument (schema 2)
├── id, mapId, width, height          ← 自 header 摘要，供 UI
├── header[18]                        ← BTL v2 头（导出时回写计数）
├── sections: { name → bytes }        ← factions, ownership, sites, armies, opaque*, markets/tavern 区
├── extensionTrailer                  ← EW4XBTL 扩展段（JURI/VICT/CNTR…）
└── layers                            ← 编辑器主路径
    ├── factions[], sites[], armies[], events[]   ← 与 Legacy* 记录 1:1
    └── extensions?                     ← 后续：结构化 JURI/VICT（仍经同一 Codec 写尾）
```

**目标形态**：仅 `scenarios/{id}.json`（schema 2 嵌入文档）。**兼容**：schema 1 桩 + `legacy.btl`；可选侧车 `*.ew4scenario.json` 在迁移期仍可读。

## 3. 接口（Content + Authoring）

**编解码（Content）** — `ScenarioBtlCodec` / `ScenarioDocumentJson` / `ScenarioBtlRoundtrip`  

**工具库（Authoring）** — `IScenarioEditToolkit`：文件读写、Pack 打开、Workspace 保存、往返校验；**CLI 与将来网页编辑器只调 Toolkit**。

```csharp
public interface IScenarioEditToolkit  // Ew4.Editor
{
    ScenarioDocument ImportBtl(string scenarioId, byte[] btlBytes);
    byte[] ExportBtl(ScenarioDocument document, ScenarioExportMode mode = ...);
    byte[] SerializeJson(ScenarioDocument document);
    ScenarioDocument DeserializeJson(byte[] jsonBytes);
    ScenarioDocument OpenFromPack(IPackSource pack, string scenarioId);
    ScenarioSaveResult SaveToWorkspace(string workspaceRoot, ScenarioDocument document);
    ScenarioBtlRoundtrip.Result ValidateBtlRoundtrip(string scenarioId, byte[] btlBytes);
    // + Read/Write *File 便捷方法
}
```

CLI：`Ew4.Tool scenario btl-to-json | json-to-btl | roundtrip | pack-to-json`

`SaveToWorkspace` 流程:

1. `ScenarioBtlCodec.ApplyLayers(document)`
2. 写入 `scenarios/{id}.json`（`ScenarioDocumentJson`）
3. 可选：Author API 再跑 `SmokeStart` 校验

## 4. XML / 规则表（同一模式）

`def_*.xml` 与地形 `*.bin` 使用平行抽象（后续补齐）:

```csharp
public interface IAssetCodec<TModel>
{
    string LogicalKind { get; }          // 如 "original.def_army"
    TModel Import(byte[] raw);
    byte[] Export(TModel model);
}
```

- **P1 已落地:** BTL `ScenarioBtlCodec` + 字节往返 `validate-btl-roundtrip`
- **P1/P4 下一步:** `ScenarioBtlCodec` 的 **JSON 往返** `Import → Export(Import(b)) == b`（typed layers 与 sections 同步）
- **XML:** 先 `Import → 保留 opaque/原字节 → Export` 无损，再逐表开放 typed 字段

## 5. 编辑器边界（第一批）

| 可编辑 | 存储位置 |
|--------|----------|
| 势力资源/国家/变体 | `layers.factions` → `sections.factions` |
| 建筑/桥/设施 | `layers.sites` |
| 部队位置与编制 | `layers.armies` |
| 剧本事件 | `layers.events` |
| 格归属 | `sections.ownership`（后期表单；当前只读） |
| 市场/酒馆/opaque | `sections.*` 只读 |
| JURI/VICT/CNTR | `extensionTrailer` 或结构化扩展（与 ModTool apply 对齐） |

## 6. 验收

| 命令 | 含义 |
|------|------|
| `validate-btl-roundtrip` | `LegacyBattle.Read` 克隆保真（无 JSON） |
| `validate-scenario-document-roundtrip` | `BTL → ScenarioDocument → BTL` 字节一致 |
| `validate-mod-example` | 叠加 rewrite 后 `SmokeStart` |

JSON 往返全库 95 剧本在 `validate-scenario-document-roundtrip` 全绿后，才开放 Workspace **保存到 Pack** 的 UI。
