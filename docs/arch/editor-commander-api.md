# 将领创作（Editor API）

状态：**v1**（技能 legacy_id + 五维适性；主菜单内嵌 UI）。  
实现：`Ew4.Editor.Commander` · 测试：`Ew4.Test.Editor.CommanderEditorTests`。

---

## 1. 范围（v1）

| 支持 | 不支持 |
|------|--------|
| 列出合并后生效的将领表 | 新增将领行 |
| 改 **skill1–skill4**（技能表 `legacy_id`，见 catalog） | 新增技能定义 |
| 改 **步兵/骑兵/炮兵/海军/要塞适性**（与指挥部将领面板一致） | 改军衔/爵位/装备/移动力等 |
| 写回 `assets/tables/def_commander.xml` | 直接改激活包 |

**不要**把 XML 里的 `star` 当成关卡评星或指挥部「星级」——引擎载入时用的是 `infantry`/`cavalry`/… 适性列；`star` 列当前 **未参与** `OriginalRuntimeCatalog` 解析。

技能 ID：`def_commander.xml` 的 `skillN` 整数 = `catalog.json` → `skills[].legacy_id`（UI 显示 `names.zh_CN`）。

---

## 2. 服务接口（C#）

```csharp
CommanderTableView List(string workspaceId);
CommanderEntry Get(string workspaceId, int legacyId);
CommanderSkillCatalogView SkillCatalog(string workspaceId);
CommanderEditResult Apply(string workspaceId, CommanderEdit edit);
```

`CommanderEditLoadout` / `CommanderEditSkills` / `CommanderEditAptitudes` 见 `CommanderModels.cs`。

---

## 3. HTTP

| 方法 | 路径 | 行为 |
|------|------|------|
| GET | `.../commanders` | 列表 |
| GET | `.../commanders/skills` | 技能 catalog（legacy_id + 显示名） |
| GET | `.../commanders/{legacyId}` | 单条 |
| POST | `.../commanders/{legacyId}/edit` | body：skill1–4 + infantry/cavalry/artillery/warship/fort |

---

## 4. 激活与主菜单

`user/active.json` 须含 **`modId`**（兼容旧字段 **`packId`**）。若只写 `packId` 且未被读取，会导致启动时误激活 base 或激活失败，进而 **`/api/catalog` 连接失败**。

---

## 5. 决策记录

| 日期 | 决策 |
|------|------|
| 2026-10-23 | v1 编辑字段对齐指挥部：适性 + 技能 legacy_id，去掉误导性的「星级」 |
| 2026-10-23 | `ActivePackStore` 同时读写 `modId` 与 `packId` |
