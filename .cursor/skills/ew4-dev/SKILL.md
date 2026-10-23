---
name: ew4-dev
description: >-
  Build and run EW4 rewrite via scripts/ew4.sh. Maintain content packs under repo mods/; sync to install/web/mods and install/apk/mods on build/install/web.
---

# EW4 开发

结构见 [docs/arch/README.md](../../../docs/arch/README.md)；任务 [docs/task/todo.md](../../../docs/task/todo.md)；计划 [docs/task/plan/roadmap.md](../../../docs/task/plan/roadmap.md)。**勿随意改 `docs/development/`。**

```bash
scripts/ew4.sh build -c Release
scripts/ew4.sh install              # publish → install/web/app
scripts/ew4.sh web                  # Ew4.Backend.Web，--home install/web，:5184
scripts/ew4.sh test -c Release
scripts/ew4.sh validate smoke       # 可选：mods/base 全包 SmokeStart
scripts/ew4.sh fmt --check
```

## 运行栈（以代码为准）

- **接入层**：`src/Ew4.Backend.Web`（`scripts/ew4.sh web`）；**服务层** `Ew4.Service`；**平台层** `Ew4.Platform.*`。
- **内容**：维护在 `mods/<packId>/`；Web 运行时 `install/web/mods/`；APK 渠道 `install/apk/mods/`（`sync-mods` 默认同步两侧）。激活与存档在对应渠道的 `user/`。
- **前端**：`src/Ew4.Front.Web/wwwroot`；包资源 `/content/*`。
- **游玩 API**：`POST /api/{route}?q=…`（`Ew4.Service`）；安装/切换 Mod 走 **HTTP**（`/api/mod-import` 等），**无**独立 shell「import mod」脚本。
- **编译**：仅 Docker dev 镜像（`scripts/ew4.sh build` → `Ew4.sln`）。

## 目录

| 路径 | 作用 |
|------|------|
| `mods/base/` | 原版内容包（本地维护，不进 Git） |
| `mods/<id>/` | 其它 Mod（APK 导入、手工放置） |
| `install/web/` | Web 渠道安装根（`app/`、`mods/`、`user/`） |
| `install/apk/` | APK 渠道安装根（与 Web 隔离） |
| `maps/` | 策划 JSON（进 Git） |
| `data/import-seed/` | 可选；仅 `import-base` 从原料**重建** `mods/base` 时用 |
| `backup/` | 封存对照，不参与构建 |

可选：`scripts/ew4.sh import-base`（`data/import-seed` → `mods/base`）、`check btl`。

命令全集：`scripts/ew4.sh help`。`dev/` 下 `paths.sh`、`docker.sh`、`cli.sh` 等仅被 `ew4.sh` source，见 `scripts/README.md`。
