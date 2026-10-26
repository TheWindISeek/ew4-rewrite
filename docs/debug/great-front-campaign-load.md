# 大前线战役载入失败与原版 catalog Error

| 字段 | 值 |
|------|-----|
| 日期 | 2026-10-23 |
| 状态 | 已修复 |
| 影响 | Mod `great-front`、切 **base** 后 catalog Error；对照 `huangkai` 正常 |

> 注：本文是历史排障记录。文中的 `ScenarioBtlResolver` / `legacy-btl` 读取路径已在 content-json-redesign 步骤 5 中删除，Content 现在只读 schema 3 场景 JSON。

## 现象

1. **主菜单**：底部出现 `Error` + 重试；`initializeClient` 里 `rpc('catalog')` 失败，整页无法进入正常菜单。
2. **大前线 → 战役 → 章节（如「帝国雄鹰」）**：
   - 右侧关卡列表为空或明显少于 `def_battlelist.xml`；
   - 中间描述区白块、战略地图标点缺失；
   - 底部确认钮存在但无法选关开局。
3. **革命之火**：前几章 json 较全，表现正常，容易误判为「仅大前线数据坏了」。
4. **切回原版（base）**：若仍走 catalog 校验失败，主菜单 Error 依旧（与 overlay 无关）。

## 根因（分层）

### A. 剧本 id 只来自 `scenarios/*.json`

`PackScenarioIds.List` 原先只枚举子包 `scenarios/*.json`。大前线 overlay 约 **45** 个 json，而 `def_battlelist.xml` 约 **98** 条战役。

`ClientContent` 的 `Scenarios` 由 `PackScenarioIds` 驱动；前端 `menus.js` 的 `openChapter` 对每个 `presentation` 要求：

```js
const scenario = this.catalog.scenarios.find(s => s.id === presentation.id);
if (!scenario) continue;
```

因此 **没有进入 catalog 的 battlelist 条目在 UI 上被直接跳过**（第 3 章起大量关卡在 base 有 BTL、overlay 无 json 时尤其明显）。

### B. `legacy-btl` 无 json 时无法读 BTL

`mods/great-front/mod.json` 声明 `scenarios.storage: legacy-btl`。`ScenarioBtlResolver.OpenWithLegacyFirst` 先走 `OpenLegacyBtl`，旧逻辑 **强制存在** `scenarios/{id}.json` 且含 `legacy.btl`。

大前线许多关卡 BTL 在 **父包 base** 的 `source/original/*.btl`，overlay 未提供 json 索引时，即使 catalog 补全了 id，**开局读 BTL 仍会失败**。

### C. base 激活时 `legacy.source_changed`（catalog）

`PlaySessionHost` 对 **无 parent** 的包（base）启用 `verifySourceHashes`。overlay 导入的 `def_commander.xml` 与 `catalog.json` 中记录的 SHA 不一致时，`VerifySources` 抛错 → **catalog API 整包失败**。

部分 APK overlay 还存在 XML 格式/数值问题，需在算哈希前与构建 catalog 时 **同一套 normalize**（`LegacyTableXmlRepair.NormalizeCommanderTable`），否则「修 catalog 仍校验失败」。

### D. 战役 UI 空白（非致命但误导）

- 战略图：`menus.js` 仅用 `manifest.strategic[id]`；大前线坐标在 battlelist 的 `centerx`/`centery`，未进 API 时地图不画点。
- 描述：`Label` 失败时 `description` 为 `desc_*` 键；旧前端对 `startsWith('desc_')` 显示空字符串。

## 修复

| 改动 | 文件 | 说明 |
|------|------|------|
| battlelist + 合并包 BTL 补全剧本 id | `LegacyBattleListScenarios.cs`、`PackScenarioIds.cs` | 读 overlay/base 合并后的 `def_battlelist.xml`，若存在 `source/original/{id}.btl`（或 `assets/original` 等）则加入 id 集合 |
| 无 json 直接读包内 BTL | `ScenarioBtlResolver.cs` | `OpenLegacyBtl`：有 json 则走 `legacy.btl`；否则 `PackAssetRead.Resolve(..., original/{id}.btl)`（含父包） |
| catalog 校验与 commander 一致 | `ClientContent.cs`、`PackRuntimeBootstrap.cs` | `VerifySources` / bootstrap 对 `def_commander.xml` 先 normalize 再 SHA256 |
| 战役 API 与 UI | `ClientContent.cs`（`ScenarioPresentation`）、`menus.js` | 下发 `strategicName`、`centerX`、`centerY`；`presentationDescription` / `battleStrategicDefinition` |
| 测试 | `LegacyBattleListScenariosTests.cs`、`ScenarioBtlResolverTests.cs` | 合并包 battlelist 数量；`legacy-btl` 无 json stub 仍可开 BTL |

**未采用**：为 great-front 手工批量生成 50+ 个最小 `scenarios/*.json`（逻辑上可由 battlelist + 直接 BTL 覆盖，减少维护负担）。

## 验证

```bash
scripts/ew4.sh build -c Release
scripts/ew4.sh sync-mods    # mods/ → install/mods/
scripts/ew4.sh web          # http://127.0.0.1:5184
scripts/ew4.sh test -c Release
```

可选 HTTP 冒烟（需 web 已起）：

```bash
scripts/dev/test-mod-switch.sh
```

人工检查：

1. 激活 **great-front** → 战役 → 各章（尤其第 3 章）关卡列表数量合理，可选关、描述/地图非空，能开局。
2. 激活 **base**（或清空 Mod）→ 主菜单无 catalog Error。
3. **huangkai** 仍正常（回归）。

## 追加（2026-10-23 晚）

### 「皇帝的凯旋」主菜单 `连接失败 (500)`

Mod 列表里 **huangkai** 的显示名即为「皇帝的凯旋」（不是战役章节名）。激活后 `catalog` 500 时，日志多为：

`LegacyBattleListScenarios.TryReadBattleList` → `XmlException: Data at the root level is invalid`

**原因**：该包 `assets/tables/def_battlelist.xml` 带 **UTF-8 BOM**。`LegacyBattleListScenarios` 用 `UTF8.GetString` + `XDocument.Parse` 未剥 BOM；而 `ClientContent` 用 `StreamReader(..., detectEncodingFromByteOrderMarks: true)` 正常。

**修复**：`LegacyBattleListScenarios.ParseUtf8Xml` 与 StreamReader BOM 行为对齐；解析失败时尝试下一路径而非整包崩溃。

### 一战 Mod 战役选章右上角两条空白丝带

**原因**：`def_battleline.xml` 有 **8** 条 `line`，其中 `hide="1"` 的隐藏章（如 `rome`、`others`）仍进入 `catalog.chapters`；`manifest.layouts.form_selcampaign` 只有 **6** 个 `btn_selcamp_*` 槽位。多出来的章走 `custom-chapter` 样式叠在右侧（`button_choosestage_france.png` 丝带底图），无地图锚点，看起来像空白 UI。

**修复**：构建 `Chapters` 时跳过 `hide="1"` 的 battleline（与隐藏关卡逻辑一致；`presentation.chapter` 仍按 `campaignN_*` 编号，不受影响）。

### 自测记录（2026-10-23）

本地已 `build -c Release` → `sync-mods` → 释放 `:5184` 后 `scripts/ew4.sh web`。

| 检查项 | 结果 |
|--------|------|
| `scripts/dev/test-mod-switch.sh`（ww1 / huangkai / great-front） | 通过：catalog、HQ、`start` 均无 500 |
| huangkai `catalog` | `ok`，89 个剧本；主菜单无「连接失败」 |
| ww1 战役选章 | `chapters=5`（隐藏章已剔除）；浏览器仅见 5 个有标题章按钮，右上角无空白 `custom-chapter` 丝带 |

若仍看到旧 UI：硬刷新（`menus.js` / 前端缓存）并确认连的是刚重启的 `http://127.0.0.1:5184`。

## 运维提示

- 浏览器可能缓存旧 `menus.js`：硬刷新或清缓存后再看战役页。
- `:5184` 被旧容器占用时：`fuser -k 5184/tcp` 或停掉旧 `docker run` 后重新 `scripts/ew4.sh web`。
- 运行时读的是 **`install/mods/`**（由 `sync-mods` / `build` / `web` 从 `mods/` 同步），改 repo 内 `mods/` 后需同步或重启 web。

## 相关代码索引

- 战役列表 UI：`src/Ew4.Front.Web/wwwroot/menus.js`（`openChapter`、`chooseBattle`、`draw`）
- Catalog：`src/Ew4.Service/ClientGame.cs` → `/api/catalog`
- 会话与校验：`src/Ew4.Service/PlaySessionHost.cs`（`verifySourceHashes: Parent 为空`）
- 包路径映射：`src/Ew4.Platform.Content/Build/PackAssetRead.cs`
