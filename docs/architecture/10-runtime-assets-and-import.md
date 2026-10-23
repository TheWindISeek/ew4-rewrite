# 10 运行时资源与导入管线

状态:已实现（与 `refactor/pack-architecture` 对齐）

## 1. 三类数据

| 类别 | 路径 | 用途 |
|------|------|------|
| **导入种子** | `data/import-seed/` 等 | 可选；`import-base` 重建 `mods/base`（不进 Git） |
| **内容包（维护）** | 仓库根 `mods/<id>/` | 维护者编辑、CLI 校验、`import-base` / 离线 APK 导入落盘 |
| **内容包（运行时）** | `install/mods/<id>/` | `EW4_HOME` 下；由 `build` / `install` / `web` / `sync-mods` 从 `mods/` **拷贝** |
| **草稿** | `build/workspaces/*` | 创作 overlay（见 [05](05-authoring.md)） |

用户状态：`install/user/`（`active.json`、存档）。前端静态资源：`Ew4.Front.Web/wwwroot/`。

## 2. 包格式与导入入口

| 扩展名 | 行为 |
|--------|------|
| `.ew4mod` / `.zip`（含 `pack.json`） | **Web**：`POST /api/mod-prepare` → `mod-import` → 校验后写入 `install/mods/<id>/` |
| `.apk` | **应用内不支持**（D1）。维护者用 **`Ew4.Tool import-apk-overlay`** 离线生成 `mods/<id>/`，再同步到 `install/` |

## 3. 代码归属（`src/` + `scripts/`）

| 能力 | 位置 |
|------|------|
| Pack 合并/校验、运行时 BTL 解析 | `Ew4.Platform.Content`（`ScenarioBtlResolver`、`PackInstallGuard` 等） |
| 游玩 API | `Ew4.Service`（路由契约 `Ew4.Interface`） |
| Web 宿主 | `Ew4.Backend.Web` + `Ew4.Platform.Runtime`（`HostApp`、zip 导入） |
| 离线：`import-base`、APK overlay、剧本嵌入、G4/G6 | `Ew4.Tool` / **`Ew4.Tool.Import`**（**不是** Content 对外 API） |
| Shell 封装 | `scripts/ew4.sh`（`import-base`、`validate`、`check btl|json-btl`、`sync-mods`） |

路径常量：`Ew4.Platform.Content.Layout.RepositoryPaths`、`Ew4.Platform.Runtime.Layout.InstallLayout`。

## 4. Web 导入（zip）

`POST /api/mod-prepare` → `POST /api/mod-import` → `Activate`。安装目标为 **`install/mods/`**（与仓库 `mods/` 独立；维护者本地常以 `mods/` 为源再拷贝）。

## 5. 命令

```bash
scripts/ew4.sh import-base          # data/import-seed → mods/base
scripts/ew4.sh sync-mods            # mods/ → install/mods/
scripts/ew4.sh validate smoke
scripts/ew4.sh check btl
scripts/ew4.sh check json-btl       # G6
dotnet run --project src/Ew4.Tool -- import-apk-overlay --apk … --id … --name …
```

Mod 制作文档：`docs/import/mod-kit/`（若有）；索引见 [import/README.md](../import/README.md)。
