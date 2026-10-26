# Editor 接口说明（场景 JSON schema 3）

本文是 `/api/editor/*` 的对外契约，以及它背后的场景 JSON 格式。实现位置见 [operations.md](./operations.md)、[interface-contracts.md](./interface-contracts.md)；设计背景见 [content-json-redesign.md](./content-json-redesign.md)。

约定：

- 所有请求/响应的 JSON 键为 **camelCase**。
- Editor 只读写 **JSON**。btl 只存在于 Mod 的转换器里（`IScenarioBtlConverter`），Editor 仅经该接口转发字节。
- 接口无状态：每个请求自己打开场景、改完即落盘，没有"会话对象"。

## 1. 概念

### 1.1 workspace

`mods/…` 之外的创作目录（`<install>/user/workspaces/<id>/`）。创建时从某个 mod 拷贝全部文件，写入新的 `mod.json`（`parent` 指向 base），再写 `.workspace.json` 元数据。读取内容时看到的是 **workspace + parent 合并后的视图**。

### 1.2 编辑模式与能力集

模式在**创建 workspace 时**确定，存进 `.workspace.json`，之后不变。

| | `baseTweak`（默认） | `free` |
|---|---|---|
| 意图 | 在 base 已有内容上增删改 | 自己安排场景与地图 |
| `editCatalog` | ✔ | ✔ |
| `editScenarioUnits`（部队、据点、势力） | ✔ | ✔ |
| `editEvents` | ✔ | ✔ |
| `editMap`（换地图/裁剪） | ✘ | ✔ |
| `createScenario`（新建场景） | ✘ | ✔ |

被禁止的操作返回错误码 `editor.capability`。`free` 选地图只能选 **base** 里的地图。

### 1.3 错误响应

| 情形 | HTTP 状态 | 响应体 |
|---|---|---|
| 内容/规则错误（`ContentError`、`ModError`、`RuleViolation`） | 400 | `{"code": "...", "message": "..."}` |
| 参数缺失、非法路径等 | 400 | `{"code": "client.failure", "message": "..."}` |
所有失败（含 `ICampaignEditorOperations` 系列：campaign get/put/edit/create/roundtrip、commanders、btl-to-json）统一为 **400** + `{"code","message"}`。

## 2. Workspace

### `GET /api/editor/workspaces`
返回 `WorkspaceInfo[]`：

```json
[{ "id": "my-ws", "name": "My workspace", "parentModId": "base",
   "createdAt": "…", "lastValidatedAt": null, "lastValidationLevel": null, "mode": "baseTweak" }]
```

### `POST /api/editor/workspaces`
Body：`{ "id": "my-ws", "name": "…", "fromModId": "base", "mode": "free" }`。`fromModId` 省略为 `base`；`mode` 省略为 `baseTweak`，非法取值返回 `workspace.mode`。返回 201 与 `WorkspaceInfo`。

错误：`workspace.name`（名称为空）、`workspace.exists`（已存在）。

### `DELETE /api/editor/workspaces/{id}`
204。不存在返回 `workspace.missing`。

### `GET /api/editor/workspaces/{id}/capabilities`
```json
{ "mode": "free",
  "capabilities": { "editCatalog": true, "editScenarioUnits": true, "editMap": true,
                    "createScenario": true, "editEvents": true } }
```
前端应据此决定显示哪些编辑入口。

### `POST /api/editor/workspaces/{id}/validate?level=`
`level` ∈ `Schema | Reference | Constraint | SmokeStart`（默认 `SmokeStart`）。返回诊断数组：

```json
[{ "severity": "Warning", "code": "mod.client.missing", "path": "client/manifest.json", "message": "…", "hint": "…" }]
```
`severity` ∈ `Info | Warning | Error`。Warning 不阻止加载与游玩。

### `POST /api/editor/workspaces/{id}/export?level=&fileName=&standalone=`
先按 `level` 校验，有 Error 则 `{success:false, archivePath:null, diagnostics}`；否则打 `.ew4mod` 到 `exports/`。

- `standalone=false`（默认）：只导出 workspace 自己的文件，**仍依赖 parent**。
- `standalone=true`：合并 parent 内容并从 `mod.json` 去掉 `parent`，导出后无需 base。
- 归档不含 `exports/` 与 `.workspace.json`。

返回 `{ "success": true, "archivePath": "…", "diagnostics": [...] }`。

### `GET /api/editor/workspaces/{id}/campaigns`
按模式分组的场景 id：`{ "campaign": [...], "conquest": [...], "tutorial": [...], "other": [...] }`。自由编辑新建的场景（id 不以 campaign/conquest/tutorial 开头）落在 `other`。

### `GET|PUT /api/editor/workspaces/{id}/files/{path}`
读/写 workspace 合并视图中的文件。`PUT` 会按内容种类校验；`mod.json`、`.workspace.json` 不可写（`workspace.path`）。

### 预览
`POST …/preview/enter`、`POST …/preview/refresh`、`POST /api/editor/preview/exit`、`GET /api/editor/preview`：切换/查询"用 workspace 合并结果游玩"的绑定，返回预览状态 JSON。

## 3. 地图目录

只列 **base** 的地图。`hash` 是地形数据的 SHA-256（十六进制小写）。

### `GET /api/editor/workspaces/{id}/maps`
```json
[{ "mapId": 1, "name": "europe", "width": 79, "height": 68, "hash": "cbab47e2…" }]
```

### `GET /api/editor/workspaces/{id}/maps/{mapId}`
单个 `{mapId, name, width, height, hash}`；不存在返回 `map.unknown`。

### `GET /api/editor/workspaces/{id}/maps/{mapId}/preview?x=&y=&w=&h=`
`x/y/w/h` 四个参数齐全才算裁剪，省略为整图。越界或地图不存在返回 `map.crop`。

```json
{ "mapId": 1, "crop": { "x": 0, "y": 0, "w": 2, "h": 2 },
  "cells": [{ "backgroundKind": 1, "backgroundIndex": 6, "foregroundKind": 11,
              "foregroundIndex": 255, "riverEdges": 0 }, …] }
```
`cells` 按行优先，长度 `w*h`。

## 4. 场景

### `GET /api/editor/workspaces/{id}/campaign/{scenarioId}`
返回场景 JSON（schema 3，格式见 §6）。场景不是 schema 3 返回 `scenario.schema`（需先用 `scenario to-v3` 转换）。

### `PUT /api/editor/workspaces/{id}/campaign/{scenarioId}?smoke=`
整份保存场景 JSON。校验：schema 为 3、`id` 与路由一致（`scenario.id`）、能被开局管线读取（`scenario.invalid`）、地图引用合法（§7）。`smoke=true` 时再做一次开局冒烟。

```json
{ "documentPath": "scenarios/<id>.json", "diagnostics": [] }
```
`diagnostics` 仅在 `smoke=true` 时出现。

### `POST /api/editor/workspaces/{id}/campaign`
**新建空场景**（仅 `free` 模式；`baseTweak` 返回 `editor.capability`）。

```json
{ "id": "my_free", "name": "Mine", "mapId": 1, "crop": { "x": 13, "y": 30, "w": 21, "h": 21 } }
```
`name` 省略取 `id`；`crop` 省略为整图。返回 201 `{ "scenarioId": "my_free", "documentPath": "scenarios/my_free.json" }`。

错误：`scenario.id`（id 须匹配 `[a-z][a-z0-9_]{0,63}`）、`scenario.exists`、`map.unknown`、`map.crop`、`editor.bad_request`（body 缺字段/类型不对）。

新场景只有地图引用和全无主归属；势力、据点、部队、事件、市场、酒馆、环境均为空数组；`settings` 为 `{objectivesDisabled:0, roundLimit:30, ratingRound:0}`。

### `POST /api/editor/workspaces/{id}/campaign/{scenarioId}/edit`
结构化编辑，改完整体校验后落盘。校验失败则**文档不变**。Body 的 `type` 决定形态；`index` 是数组下标：

| `type` | Body | 能力 | 说明 |
|---|---|---|---|
| `armyCell` | `{index, cell}` | `editScenarioUnits` | 移动部队 |
| `siteCell` | `{index, cell}` | `editScenarioUnits` | 移动据点 |
| `factionResources` | `{index, money, industry, food}` | `editScenarioUnits` | 三项资源一起设 |
| `army` | `{index, fields}` | `editScenarioUnits` | 见下 |
| `site` | `{index, fields}` | `editScenarioUnits` | 见下 |
| `faction` | `{index, fields}` | `editScenarioUnits` | 见下 |
| `event` | `{index, fields}` | `editEvents` | 见下 |
| `map` | `{fields:{mapId, width?, height?}}` | `editMap` | `width/height` 必须等于当前裁剪尺寸，否则 `scenario.patch.map_size`（暂不支持改尺寸） |
| `mapSelect` | `{fields:{mapId, x?, y?, w?, h?}}` | `editMap` | 换地图/裁剪，见 §7；`x,y,w,h` 全给才算裁剪，省略为整图 |

`fields` 里所有键都可选，**缺省或为 `null` 表示不改**。键名与范围（超范围返回 `scenario.patch.range`）：

| 对象 | 键 | 类型/范围 |
|---|---|---|
| army | `cell`, `commander`, `health`, `maximumHealth` | 16 位有符号整数 |
| | `unit`, `grade`, `movement`, `morale`, `moraleDuration` | −128…127 |
| site | `cell`, `cityId` | 16 位有符号整数 |
| | `buildingKind`, `buildingLevel` | 0…255 |
| | `installationKind` | −128…127 |
| faction | `money`, `industry`, `food`, `camp` | 32 位整数 |
| | `countryId`（写入 JSON 的 `country`）, `variant` | 字符串，UTF-8 ≤ 15 字节 |
| | `ai` | 布尔 |
| event | `id`, `condition`, `effect`, `round`, `dialogue`, `cell` | 32 位整数 |
| | `country` | 字符串，UTF-8 ≤ 15 字节 |

下标越界返回 `scenario.patch.armies|sites|factions|events`。成功返回：

```json
{ "scenarioId": "campaign1_01", "documentPath": "scenarios/campaign1_01.json" }
```

### `POST /api/editor/workspaces/{id}/campaign/{scenarioId}/roundtrip`
检查场景能否稳定地转成游戏二进制格式再转回（经 Mod 转换器）。

```json
{ "stable": true, "sourceLength": 3853, "exportedLength": 3853 }
```

## 5. 将领表

`GET …/commanders`、`GET …/commanders/skills`、`GET …/commanders/{legacyId}`、`POST …/commanders/{legacyId}/edit`。不在本次改动范围，字段见 `Ew4.Interface/Editor/CommanderModels`。

## 6. 场景 JSON（schema 3）

顶层字段：

| 字段 | 说明 |
|---|---|
| `schema` | 固定 `3` |
| `id` | 场景 id，必须与文件名、路由一致 |
| `name` | 显示名（可选） |
| `map` | 地图引用，见 §7 |
| `settings` | `objectivesDisabled`（非 0 表示关闭"目标点"胜利）、`roundLimit`（回合上限）、`ratingRound`（评级回合） |
| `ownership` | 每格归属，见下 |
| `factions` `sites` `armies` `events` `markets` `taverns` `environments` | 记录数组，字段见下 |
| `extensions` | 可选：`JURI`（辖区）、`VICT`（胜利规则）、`CNTR`（国家 id） |
| `extras` | 仅供 Mod 导出 btl；Content/Editor 忽略 |

**`ownership`**：`{ "rows": ["…", …], "raw"?: "base64" }`，`rows` 共 `crop.h` 行、每行 `crop.w` 个字符。`.` 无主；`0-9`、`a-z`、`A-Z` 依次表示势力序号 0…61；`?` 表示其它值（取自 `raw`）。

**记录字段**（数字均为整数，除非注明）：

| 数组 | 字段 |
|---|---|
| `factions` | `money` `industry` `food`、`ai`(bool)、`camp`、`survivalMode`、`country`、`variant`、`incomeMultiplier`(浮点)、`aiEvasionBonus`(浮点)、`color`、`values`(26 个整数)、`count172`、`state176` |
| `sites` | `cell` `cityId` `buildingKind` `buildingLevel` `state6` `state7` `state8` `installationKind` `marker` `state11` `bridgeStyle0` `bridgeStyle1` `bridgeStyle2` `state15` |
| `armies` | `cell` `unit` `grade` `health` `maximumHealth` `experience` `veterancy` `movement` `morale` `moraleDuration` `commander` `flag58` `objective` `behavior` `facing` `transport` `acted` `forceRecovery` `flag5c` `state70` `constructionTurns` `behaviorExpiry` `restoreBehavior` `burning` |
| `events` | `id` `condition` `effect` `round` `dialogue` `cell`、`country`、`triggered`(0/1)、`padding41` `padding42` `padding43` |
| `markets` | `cell`、`slots`(14 项，每项 `{itemId, quantity}`) |
| `taverns` | `cell`、`candidates`(≤5 项，每项 `{commanderId, money, industry, medals, availableRound}`) |
| `environments` | `cell` `intensity` |

`state*`、`flag*`、`padding*` 等是尚未弄清含义的字节，原样保留；以后会改成有意义的名字（见 content-json-redesign §9）。

**`cell` 是整张地图的格下标**（`y * 地图宽 + x`），不是裁剪内的下标；`ownership` 才按裁剪区域排列。

## 7. 地图引用

```json
"map": { "source": "base", "mapId": 1, "hash": "cbab47e2…",
         "crop": { "x": 13, "y": 30, "w": 21, "h": 21 } }
```

- `source` 目前只能是 `base`，否则 `map.source`。
- `hash` **可选**。存在且与当前 base 地图不一致时给 **Warning** `map.hash_mismatch`（地图自制作场景后变过），**不阻止**加载与游玩。
- 地图不存在 `map.unknown`；裁剪越界 `map.crop`（均为 Error）。
- `mapSelect` 的副作用：写入当前 `hash`；地图或尺寸变化时 `ownership` 重置为全无主；现有部队/据点必须仍在新裁剪内，否则 `map.cells`（先移动它们）。

## 8. 已知问题与待定

1. 已处理：失败统一 400；删除 `legacy*` 路由（`/api/editor/campaign/*`）、`btlPath`、`ValidateRoundtrip(originalBtl)`、`CommitScenarioDocument`；`btl-to-json` 保留，只接受 multipart（`id` + `file`），不再支持服务器本地路径。
4. 改裁剪**尺寸**只能通过 `mapSelect`；`map` 编辑里的 `width/height` 暂不支持改变。
5. workspace 创建时会拷贝 base 的全部文件，overlay 导出体积约 100 MB。
6. 同一 workspace 第二次导出会因校验把 `exports/` 当未知路径失败。
7. 前端暂无选模式、选地图、新建场景的界面。
