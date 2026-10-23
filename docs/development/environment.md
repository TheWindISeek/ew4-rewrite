# 开发环境

状态:已实现

**不在宿主机安装 .NET。** 只用 Docker + **`scripts/ew4.sh`**（`.config/docker/Dockerfile.dev` → `mcr.microsoft.com/dotnet/sdk:10.0`）。

## 目录（运行时）

| 路径 | 用途 | Git |
|------|------|-----|
| `mods/` | 内容包维护：`base/` + 其它 `<id>/` | 忽略（仅 `mods/README.md` 跟踪） |
| `install/mods/` | 运行时内容包（从 `mods/` 拷贝） | 忽略 |
| `install/user/` | `active.json`、存档 | 忽略 |
| `maps/` | 策划 JSON | 跟踪 |
| `build/` | MSBuild bin/obj | 忽略 |
| `install/` | 发行 publish（日常可忽略） | 忽略 |

Web 启动时 **`--root` = 仓库根**、**`--home` = `install/`**（或 `EW4_HOME`）；读写 `install/mods/` 与 `install/user/`。

## 日常命令

```bash
scripts/ew4.sh image dev    # 首次；build/web 也会自动构建 ew4-dev:local
scripts/ew4.sh build        # Ew4.sln
scripts/ew4.sh test
scripts/ew4.sh web          # http://127.0.0.1:5184 ，Ew4.Backend.Web 开发态 dotnet run
```

```bash
scripts/ew4.sh validate smoke    # mods/base 全包 SmokeStart
scripts/ew4.sh smoke-start
scripts/ew4.sh check btl
scripts/ew4.sh check json-btl
scripts/ew4.sh import-base       # data/import-seed → mods/base
scripts/ew4.sh sync-mods         # mods/ → install/mods/
scripts/ew4.sh fmt --check
```

Mod 安装/切换：**Ew4.Backend.Web HTTP**（`/api/mod-import` 等），无 shell「import mod」脚本。

日常**不用** publish 镜像 `ew4-web`（`.config/docker/Dockerfile`，留给 CI/部署）。

## 环境变量

| 变量 | 含义 |
|------|------|
| `EW4_HOME` | 安装根（默认 `<repo>/install`；运行时 mods 在 `install/mods`，用户在 `install/user`；维护者在仓库 `mods/`） |
| `EW4_IMPORT_SEED` | `import-base` 原料目录（默认 `data/import-seed`） |
| `PORT` | `web` 端口（默认 5184） |
| `EW4_DEV_IMAGE` | dev 镜像名（默认 `ew4-dev:local`） |

脚本模块划分见 [scripts/README.md](../../scripts/README.md)。路径常量代码：`Ew4.Platform.Content.Layout.RepositoryPaths`、`Ew4.Platform.Runtime.Layout.InstallLayout`。

## 浏览器烟测（改 Play / Host 后）

1. `scripts/ew4.sh web`，打开 `http://127.0.0.1:5184`，控制台无致命错误。
2. 新游戏 → 进地图，首回合可操作。
3. 移动 → 结束回合 → AI 不卡死。
4. 存档 → 刷新 → 读档一致。

失败时记：控制台日志、API 的 `code`/`message`、`revision`。
