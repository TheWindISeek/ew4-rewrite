# mods/ — 内容包

本目录是 **内容包维护区**（不进 Git，仅本 `README.md` 跟踪）。运行时读 **`install/mods/`**（由 build / web 或手动把 `mods/` 同步过去）。存档与当前包：`install/user/active.json`。

规范说明（**当前实现**：根目录 **`pack.json` + 推荐 `mod.json`**；合并为单文件见 [docs/task/plan/pack-manifest-unify.md](../docs/task/plan/pack-manifest-unify.md)）。

---

## 目录约定

| 路径 | 说明 |
|------|------|
| `mods/base/` | 原版包，宿主默认激活 |
| `mods/<packId>/` | 其它 Mod，与 `base` 同级；`pack.json` 的 `id` 与目录名一致 |

策划用 JSON（如地图）在仓库 **`maps/`**，不要放进 `mods/`。

---

## 符合需求的包长什么样

### 1. 根清单（当前：两个文件）

**`pack.json`（必填）** — 安装、激活、zip 导入、父子继承都认它。

```json
{
  "schema": 1,
  "id": "base",
  "name": "EW4 Rewrite Base",
  "version": "0.0.0",
  "parent": null,
  "requires": { "engine": ">=0.3.0" },
  "formats": {
    "scenario": 2,
    "rules": 1,
    "map": 1,
    "commander": 1,
    "unit": 1,
    "country": 1,
    "text": 1,
    "script": 1,
    "asset": 1,
    "source": 1,
    "extension": 1
  }
}
```

差异 Mod 示例：把 `id` / `name` 改成你的包，并设 **`"parent": "base"`**（只允许 **一层** parent，parent 不能再有 parent）。

**`mod.json`（强烈推荐）** — 声明剧本怎么加载（`PackModProfile`）。**目标形态**是嵌入 JSON，而不是包内裸 BTL：

```json
{
  "schema": 1,
  "scenarios": {
    "storage": "embedded-json",
    "documentSchema": 2
  }
}
```

| `scenarios.storage` | 含义 |
|---------------------|------|
| `embedded-json` | 优先 `scenarios/{id}.json` 完整剧本文档，失败再回退 legacy BTL |
| `legacy-btl` | 优先 BTL / 索引桩（APK 差异包过渡期） |
| 省略整个 `mod.json` | **Auto**：按每个 `scenarios/*.json` 自动判断嵌入或 legacy |

没有 `mod.json` 时 Auto 常仍可用，但 **新包请显式写 `embedded-json`**，避免和 overlay 行为不一致。

### 2. 推荐目录树（`base` 或完整包）

```text
<packId>/
  pack.json
  mod.json                    # 推荐：embedded-json
  scenarios/
    campaign1_01.json         # ScenarioDocument（嵌入 JSON，非仅 BTL 指针）
    …
  assets/
    client/                   # manifest.json、countries.json、UI 资源映射
    tables/                   # def_*.xml 等（可与 original 合并布局）
    runtime-content.json      # 规则表等（按 base 布局）
  rewrite/                    # 扩展 PNG → 浏览器 /content/extensions/…
    …
  source/
    original/                 # 可选：*.btl 语料；嵌入迁移后可删减
```

逻辑路径与磁盘路径由 `PackAssetRead` 映射（例如运行时读 `original/def_item.xml` 可能落在 `assets/tables/` 或 `assets/original/`）。

### 3. 差异包（overlay，`parent: base`）

只放 **相对 base 有改动的文件**，不必复制整包：

```text
huangkai/
  pack.json                   # id、name、parent: base
  mod.json                    # 按本包剧本策略：embedded-json 或 legacy-btl
  scenarios/                  # 仅改动的剧本 JSON
  assets/tables/              # 仅改动的 def_*.xml
  assets/client/              # 如 countries.json、manifest 片段
  rewrite/                    # 新图集/旗帜等
```

激活时与 parent **合并读取**；安装校验见 `PackInstallGuard`（P1–P8）。

### 4. 前端与资源最低期望

切换 Mod 后 UI 正常，通常还需要（在包内或 parent 合并后可见）：

| 资源 | 典型路径 |
|------|----------|
| UI / 剧本 manifest | `assets/client/manifest.json` |
| 国家表（旗、色、变体） | `assets/client/countries.json` |
| 兵种动画绑定 | `assets/client/unit-visuals.json`、`effects.json` |
| 原版表 | `assets/tables/def_*.xml` 或 `assets/original/def_*.xml` |

只改 XML 未更新 `manifest.json` / `countries.json` 时，容易出现头像、兵种卡、涂色错位（见 [docs/task/plan/mod-experience.md](../docs/task/plan/mod-experience.md)）。

### 5. 剧本 JSON 形态

- **符合需求（嵌入）**：`scenarios/{id}.json` 为 **ScenarioDocument**（`documentSchema: 2`），运行时由 `ScenarioBtlResolver` 导出 BTL 字节再进引擎。
- **过渡（legacy 桩）**：JSON 仅索引 + `legacy.btl` 路径，需 `mod.json` 的 `legacy-btl` 或 Auto；父包可提供 BTL。

校验：`dotnet run --project src/Ew4.Tool -- check json-btl`（G6）、`validate smoke`（G3）。

---

## 怎么做出这样的包

| 目的 | 做法 |
|------|------|
| 从种子重建 `base` | `Ew4.Tool import-base`（`data/import-seed` → `mods/base`）；建议再跑 `pack-convert-legacy-scenarios` 写入 **`mod.json`** |
| 旧包迁嵌入 JSON | `Ew4.Tool pack-convert-legacy-scenarios --pack mods/base` |
| APK 差异 | `Ew4.Tool import-apk-overlay --apk … --id … --name … --base mods/base --dest mods/<id>`；按需补 `mod.json` |
| 同步到运行时 | 拷贝 `mods/*` → `install/mods/`（或 build/web 自动同步） |
| 浏览器安装 zip | 根目录含 `pack.json` 的 `.ew4mod` / `.zip` → `POST /api/mod-import` |

---

## 安装与切换

- **不要**依赖 shell「import mod」脚本。
- Web：**Ew4.Backend.Web** — Mod 菜单或 `/api/mod-import`、`/api/mods`（activate）。
- 改 `mods/` 后记得同步 **`install/mods/`** 再测。

代码索引：`PackManifestReader`、`PackModProfile`、`ScenarioBtlResolver`、`HostApp`。架构摘要：[docs/arch/README.md](../docs/arch/README.md)。
