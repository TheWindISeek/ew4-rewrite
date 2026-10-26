# Content / Mod / Editor 重构设计：出了 Mod 只有 JSON

状态：**接口设计定稿（待实现）**
关联：[module-responsibilities.md](./module-responsibilities.md)、[engine-redesign.md](./engine-redesign.md)、[operations.md](./operations.md)。

---

## 1. 目标与已确定的决定

1. mod 作者不再直接修改 btl；对游戏资产的所有交互都用人类可读的 JSON。
2. **btl 只存在于 Mod 内部**：导入（btl → JSON）、导出（JSON → btl）、导出 mod 包都在 Mod。Content、Editor、Engine、Service 不出现 btl 类型。
3. `base` mod **只读**；所有编辑写入 overlay（`parent = base`）。编辑产物默认依赖 base，并提供"导出独立 mod"。
4. 自由编辑的地图**只能从 base 地图中选取**区域；地图以"引用 + hash + crop"表示，hash 不一致只告警、不禁止使用。
5. 不提供玩家自行导入 APK；发布时自带 APK 转出的 base。
6. 未来必须能导出原版可读的 btl，但分阶段实现；现阶段只做最简单的往返测试。

## 2. 数据流

```text
         ┌──────────── Mod ────────────┐
btl ───▶ │ Import: btl → ScenarioJson  │ ───▶ JSON（出了 Mod 就只有 JSON）
btl ◀─── │ Export: ScenarioJson → btl  │ ◀───
         └─────────────────────────────┘
JSON ─▶ Content（校验 + 翻译成类型化规则）─▶ Engine
JSON ◀▶ Editor（只读写 JSON）
```

- 磁盘、Mod → Content、Editor API 这几处用 JSON；Content 加载后转为类型化规则再交给 Engine，**不把 `JsonElement` 传进 Engine**。
- Mod 的转换器输入输出都是字节 / JSON 文本，**不引用 Content 的类型**，保持"Content 依赖 Mod"的方向。JSON schema 由 Content 定义并校验。
- `base` 的 JSON 场景由 Tool 在构建时用 `Mod.Import` 转一次并入库；`source/original/*.btl` 只作回归测试的转换源。
- `mod.json`：`scenarios.storage` 改为 `"json"`，`documentSchema` 升到 3。

## 3. 现状（迁移起点）

- `mods/base/scenarios/*.json` 目前只是壳：`schema`、`id`、`name`、`mapId` + `legacy.btl` 指向 btl，`mod.json` 中 `scenarios.storage = "legacy-btl"`。
- Content 开局管线（`StaticData*`）直接读 btl 二进制记录（180 字节势力行、`opaque44` 事件段、header 整数等）。
- btl 编解码分散在 Content（`Campaign/ScenarioBtlCodec`、`Importers/Btl`、`BtlScenarioImporter/Exporter`、`ScenarioBtlResolver`、`LegacyBattleListScenarios`）、Tool（`BaseModImporter`、`ModScenarioConvert`、`ApkOverlayModImporter`）、Editor（`Campaign/*`）。

## 4. 场景 JSON（schema 3）

```jsonc
{
  "schema": 3,
  "id": "campaign1_01",
  "name": "...",
  "map": { "source": "base", "mapId": 12, "hash": "sha256:…", "crop": { "x": 0, "y": 0, "w": 40, "h": 30 } },
  "factions":   [ { "id": "fra", "camp": 1, "human": true, "resources": {}, "ai": "default" } ],
  "cells":      [ { "cell": 123, "owner": "fra", "building": { "kind": "city", "level": 2 }, "objective": true } ],
  "units":      [ { "id": "...", "owner": "fra", "cell": 123, "profile": "...", "commander": "..." } ],
  "commanders": [],
  "events":     [ { "id": "...", "trigger": {}, "conditions": [], "effects": [], "dialogue": "...", "popup": {} } ],
  "victory":    { "rules": {} },
  "economy":    { "commerce": {}, "technology": {} },
  "extras":     {}
}
```

规则：

- **`victory`** 只有一段，不再区分"基础胜利 / 扩展胜利"。Content 把它翻译成一个 `VictoryRules`；Engine 只保留一个 `(state, rules) → outcome` 求值器。
- **`extras`** 仅供 Mod 导出 btl 用：放尚未语义化的 header 整数、未知段落，保证往返不丢字节；Content **完全忽略**。逐步语义化后，字段从 `extras` 迁到正式字段。
- **`map`**：`source` 目前只允许 `base`；`crop` 省略表示整图；`hash` 为被引用地图内容的哈希。校验器在 hash 不一致时产生 **Warning**（不是 Error）。

## 5. 接口

### 5.1 Mod（btl 转换与导出）

```csharp
interface IScenarioBtlConverter
{
    ConvertResult ToJson(string scenarioId, ReadOnlyMemory<byte> btl, ConvertContext context);
    ConvertResult ToBtl(JsonElement scenario, ConvertContext context);
}

interface IModExporter
{
    // 把 overlay 与其依赖的 base 内容打成独立 mod；可选同时导出 btl。
    ExportResult Export(ExportRequest request);
}
```

`ConvertResult` 含 `Json` / `Bytes` 与 `Diagnostics`。

### 5.2 Content（只读 JSON）

```csharp
interface IScenarioSource { ScenarioSpec Load(string scenarioId); }

interface IScenarioValidator
{
    IReadOnlyList<Diagnostic> Validate(JsonElement scenario);   // 含 map.hash 警告
}

interface IMapCatalog                                            // 地图选择模块
{
    IReadOnlyList<MapSummary> List();                            // base 内可选地图
    MapDescription Describe(int mapId);                          // 尺寸、地形、hash
    MapPreview Preview(int mapId, Crop? crop);
}
```

`Diagnostic` 含 `Severity (Info | Warning | Error)`、`Code`、`Path`、`Message`。**Warning 不阻止加载与游玩。**

### 5.3 Editor

```csharp
enum EditorMode { BaseTweak, Free }

record Capabilities(bool EditCatalog, bool EditScenarioUnits, bool EditMap, bool CreateScenario, bool EditEvents);

interface IEditorHost
{
    EditSessionInfo Open(string baseId, EditorMode mode);        // 玩家 ↔ 编辑者的切换点
    void Close(string sessionId);
}

interface IEditSession
{
    string Id { get; }
    EditorMode Mode { get; }
    Capabilities Capabilities { get; }
    string Base { get; }                                          // 只读
    EditResult Apply(EditPatch patch);                            // 按 Capabilities 校验
    IReadOnlyList<Diagnostic> Validate();
    PreviewHandle Preview(string scenarioId);                     // 走游玩同一条管线
    void Save();                                                  // 写 overlay（parent = base）
    ExportResult ExportMod(ExportOptions options);                // 经 IModExporter
}
```

两种模式只在能力集和初始内容上不同：

| | BaseTweak | Free |
|---|---|---|
| 初始内容 | base 的全部场景、兵、将领 | 空场景，只选地图 |
| Capabilities | `EditCatalog`、`EditScenarioUnits` | 全开；`EditMap` 仅限从 base 选区域 |
| 产物 | overlay，`parent = base` | 同左 |
| 导出 | `ExportMod` | `ExportMod` |

### 5.4 Interface 层新增操作

沿用 `IGameOperations.Invoke`，不新增 HTTP 分流：
`editor.open`、`editor.close`、`editor.apply`、`editor.validate`、`editor.preview`、`editor.save`、`editor.export`、`map.list`、`map.describe`、`map.preview`。

## 6. 迁移步骤

| # | 内容 | 验收 |
|---|---|---|
| 0 | 定稿本文档（schema 3 + 接口） | 本文 |
| 1 | `Mod.Import` 实现 `ToJson`，用 `base` 全部 btl 转换 | 最简单测试：`ToJson` 后再 `ToBtl`，与原 btl 逐字节一致 |
| 2 | Content 开局管线改读 `ScenarioSpec`；先用适配层生成现有内部结构，再逐步替换 `StaticDataBattle` | 现有开局/冒烟测试全绿 |
| 3 | Editor 切到 JSON 文档并加入 `Capabilities` | Editor 测试不再引用 btl |
| 4 | `IMapCatalog` 与自由编辑；`IModExporter` | 能新建空场景并导出 mod |
| 5 | 删除 Content / Tool / Editor 中遗留的 btl 代码，更新 `module-responsibilities.md` | 仅 Mod 引用 btl |

`ToBtl` 逐步做到"原版游戏可读"；现阶段只要求步骤 1 的往返测试通过。

## 7. 进度

| 步骤 | 状态 |
|---|---|
| 0 文档 | 已完成 |
| 1 `Mod.Import` 的 `ToJson` / `ToBtl` | **已完成最小版**：`Ew4.Platform.Mod/Scenario/ScenarioBtlConverter`。语义字段目前只有 `schema`、`id`、`map`（mapId + crop，暂无 hash）；其余无损放在 `extras.btl`（header、8 个定长分段 base64、扩展尾部）。`mods/base/source/original` 下全部 95 份 btl 往返逐字节一致（`ScenarioBtlConverterTests`）。`ToBtl` 遇到 `map` 与 extras 不一致时给 Warning 并忽略，不静默改写 |
| 2 Content 开局读 schema 3 | **适配层已完成**：`ScenarioBtlResolver.TryOpenSchema3` 是唯一接缝（schema 3 优先于旧格式检测），经 `Mod.ScenarioBtlConverter.ToBtl` 还原 `ScenarioDocument` 交给现有开局管线与 Editor。尚未做：把 `extras.btl` 逐块语义化并替换 `StaticDataBattle` |
| 2a 语义化 `factions` / `sites` / `armies` / `events` | **已完成**：`Mod/Scenario/BtlRecords` 是定长记录的字段表（偏移、类型），JSON 数组与二进制行互转；`map` 现在是权威来源（改 `mapId`/`crop` 会写回 header），数组长度同步 header 条数。记录里 JSON 表达不了的字节（字符串结束符后的 `0xCD` 填充等）写在该行的 `raw`，读回时以 `raw` 为模板、只改写与模板不同的字段，所以改一个数不会扰动其它字节。早期只有 extras 的 schema 3 文件仍可读。仍留在 `extras.btl` 的：header 其余整数、`ownership`、`opaque8`、`opaque116`（商店）、`opaque108`（酒馆）、扩展尾部 |
| 2b 语义化 `ownership` / `markets` / `taverns` / `extensions` | **已完成**：`ownership` 是字符画归属图（`.` 无主，`0-9a-zA-Z` 势力序号，非常规字节用 `?` 并以 `raw` 兜底）；`markets`（14 个 `itemId/quantity` 槽 + `cell`）、`taverns`（`candidates` 数组，有效条数由记录内计数决定 + `cell`）；btl 尾部容器变成 `extensions` 数组：`JURI`（辖区 `cellCenters` 等）、`VICT`（`rules`）、`CNTR`（`factions`、`eventCountries`），绑定 btl 主体的 SHA-256 **不进 JSON、导出时按主体重算**；不能无损按语义重建的块和未知标签保留为 `payload`（base64）。`extras.btl` 现在只剩 header 其余整数与 `opaque8`。当前语料没有任何场景带扩展块，`extensions` 由合成用例测试（Content 里真实序列化出来的块若与重建不一致会自动退回 `payload`） |
| 工具 | `Ew4.Tool scenario to-v3 --mod <dir>`：把 mod 的 `scenarios/*.json`（legacy btl 或 schema 2）原地转成 schema 3，逐份校验往返字节一致，找不到 btl（如 multiplay*、overlay 指向 parent）则跳过；`import-base` 现在直接产出 schema 3；对已是 schema 3 的文件会按当前转换器重新生成（升级） |
| 3 Editor 切 JSON | **已完成**（见 2d）。 |
| 4 地图选择 / 自由编辑 / 导出 | **已完成**（见 2e）。 |
| 5 | **已完成**：删除 Editor 侧 legacy 路由/`btlPath`/`ValidateRoundtrip`/`CommitScenarioDocument`；Content 删除 `Campaign/*`（`ScenarioBtlCodec`、`ScenarioDocument`、`Importers/Btl*`、`ScenarioBtlRoundtrip`、`LayerRoundtripDiagnostics`）、`ScenarioBtlResolver`（开局入口改为 `ScenarioModAccess.OpenBattle`，只认 schema 3，否则 `scenario.schema` 错误）、`ScenarioBtlCache`、`StaticDataBattleAssembly`、`StaticDataRecordBytes`、`StaticDataBattle.Read(btl)`；Tool 删除 `ModScenarioEmbedder`、`StaticDataSourceRoundtrip`、`ScenarioJsonBtlValidator` 及 `validate-btl-roundtrip` 等命令（`ModScenarioConvert.ToSchema3` 取代，`import-apk-overlay` 也走它）；`scripts/ew4.sh check` 已移除。入库的 `ex-*` overlay 场景改为 schema 3 整份拷贝；`mods/base` 里无 btl 的 `multiplay*` 桩文件已不再保留。残留：`StaticDataBattleExtensions` 里的 btl 扩展块写入/旧读取方法、`LegacyBattleListScenarios` 等，仅 Content 内部，待后续清理。 |

## 8. 步骤 2 说明与已知事项

| 2c（Content 直读） | Mod 转换器新增顶层 `settings`（objectivesDisabled/roundLimit/ratingRound，对应 h11–h13）与 `environments`（opaque8）；Content 新增 `StaticDataBattle.FromJson` 与 `ScenarioBtlResolver.OpenBattle`，开局管线（`ScenarioJsonBuilder`、`PlayContent.Battle`）对 schema 3 直接读语义 JSON，不再经 btl 字节；指纹改为 JSON 内容 SHA-256（旧存档作废）。测试 `StaticDataBattleJsonTests`：90 份 base 剧本 JSON 路径与 btl 路径结果逐项一致。header[15]/[17] 仍未知，游戏不使用。Editor 仍走 btl 适配层 |

| 2d（Editor 切 JSON） | Editor 不再引用 btl/`ScenarioDocument`：`EditSession` 以 `JsonObject` 持有 schema 3 场景，`ScenarioJsonPatch` 按 `Capabilities`（`EditorMode.BaseTweak`/`Free`）校验并直接改 JSON（字段范围与原记录宽度一致），每次编辑后用 `StaticDataBattle.FromJson` 整体校验，失败则不落盘；`ScenarioJsonStore` 写 workspace。HTTP 接口路径不变，但 `campaign/{id}` 现在返回 schema 3 JSON（前端 `editor.js`/`editor-in-game.js` 已适配）。BaseTweak 不允许改地图（`editor.capability`），BaseTweak 的 `EditEvents` 为真以保持原有的事件字段编辑。btl 相关接口（`ValidateRoundtrip`、`BtlToJson`、roundtrip）改走 Mod 的 `IScenarioBtlConverter`，roundtrip 报告现为 `{stable, sourceLength, exportedLength}`。Tool 的 `scenario btl-to-json/json-to-btl/roundtrip/patch-army` 同步改用转换器。**未做**：`IEditorHost`/`editor.open` 等会话 API（与第 4 步一起）、改裁剪尺寸（`scenario.patch.map_size` 暂拒） |

| 2e（地图选择、自由编辑、导出） | Content `Maps/MapCatalog`（`IMapCatalog`：`List`/`Describe`/`Preview`，hash = 地形字节 SHA-256）。场景 `map.hash` 可选：存在且与当前 base 地图不一致时 `ScenarioChecks` 给 **Warning**（`map.hash_mismatch`），不阻止；`map.source` 非 base、地图不存在、裁剪越界是 Error。编辑：`ScenarioEditMapSelect(mapId, crop?)`（需 `EditMap`，HTTP 类型 `mapSelect`）会写入 hash，尺寸/地图变化时归属重置为全无主，现有部队/据点必须仍在裁剪内（cell 是整图下标，不是裁剪内下标）；`EditSession.CreateFree` / `IScenarioEditor.Create` 在 workspace 新建空场景（需 `CreateScenario`，仅 Free 模式；id 须匹配 `[a-z][a-z0-9_]{0,63}`）。导出：`ExportOptions.Standalone`（HTTP `?standalone=true`）：默认只导出 workspace 的 overlay（仍依赖 parent），Standalone 合并 parent 内容并去掉清单里的 parent；`ExportModSource` 定格文件列表并排除 `exports/`、`.workspace.json`。**未做**：`map.list/describe/preview` 的 HTTP/Interface 操作、`IEditorHost`、导出时附带 btl。**已知**：workspace 创建时会拷贝 base 的全部文件（overlay 导出约 100 MB）；同一 workspace 第二次导出会因校验把 `exports/` 当未知路径而失败（导出功能原有问题） |

| 2f（补全编辑接口） | workspace 现在有编辑模式（创建时 `mode`: `baseTweak`（默认）/`free`，存 `.workspace.json`，`WorkspaceInfo.Mode`），`ScenarioEditor` 按 workspace 的模式取能力集。新增 HTTP / 操作：`GET workspaces/{id}/capabilities`（`editor.workspace.capabilities`）、`GET …/maps`、`GET …/maps/{mapId}`、`GET …/maps/{mapId}/preview?x&y&w&h`（`editor.map.list/describe/preview`，Content 平面，经 `IEditorWorkspaceOperations`）、`POST …/campaign`（`editor.campaign.create`，body `{id,name,mapId,crop?}`，仅 free 模式，经 `ICampaignEditorOperations`）。沿用无状态 HTTP：`editor.open/close/apply/validate/save/export` 已由现有的 get/edit/put/validate/export 覆盖，不另设会话对象（`IEditorHost` 取消）。前端暂无对应 UI。 |


接口契约文档：[editor-api.md](./editor-api.md)（第 5 步删除代码前以它为准）。

- 本仓库 `mods/base`、`mods/ww1`、`mods/huangkai`、`mods/great-front` 是生成/未入库内容，已用 `to-v3` 转换；入库的 `ex-*` 示例 overlay 已改为 schema 3 整份拷贝（只改了列表名）。
- `mods/base/scenarios/multiplay*.json` 仍是旧壳（源 btl 不在 `source/original`），沿用旧路径，未转换。
- 验证：`scripts/ew4.sh test` 全绿；真机以 schema 3 的 base 启动，战役 1/2/10 对白流程与战役 8 的 AI 多回合均正常。

## 9. 未决项

- `extras` 里剩下的 `opaque8` 与 header 其余整数（`header[11,12,13,15,17]`）的含义。
- 把 `victory` / `jurisdictions` / `countryIds` 从 `extensions` 数组提升为场景顶层的命名字段（与"合并成一个 `victory` 段"的设计对齐）。
- 字段名目前沿用二进制字段名（`state6`、`flag58` 等），含义未明的字段待逐步改成有意义的名字。
- `ToBtl` 对"原版可读"的完整程度与验证方式（需真机验证）。
- 事件 / 胜利条件 JSON 的具体字段表（随步骤 2 细化）。
