# build/ 与 install/ 布局

## 目录分工

| 路径 | 作用 | Git |
|------|------|-----|
| `build/<项目名>/` | MSBuild 的 bin/obj（`Directory.Build.props`） | 否 |
| `install/web/` | **Web 渠道**安装根（默认 `EW4_HOME`） | 否 |
| `install/web/app/` | `dotnet publish` 的 Web 宿主 + `wwwroot` | 否 |
| `install/web/mods/` | Web 运行时内容包 | 否 |
| `install/web/user/` | Web 用户数据 | 否 |
| `install/apk/` | **APK 渠道**安装根（`EW4_CHANNEL=apk`） | 否 |
| `install/apk/mods/`、`install/apk/user/` | APK 内容与存档（与 Web 隔离） | 否 |

仓库根 **`mods/`** 为内容包维护目录。`build`、`install`、`web`、`sync-mods` 会把 `mods/` 拷贝到 **`install/web/mods`** 与 **`install/apk/mods`**（可用 `sync-mods web` 只同步一侧）。

**发行游玩**：只需拷贝整个 **`install/`** 目录（`web/app` 已 publish、`web/mods` 含 `base`），运行 `install/web/run.sh`；无需 `src/` 或 `Ew4.sln`。

## 命令

```bash
scripts/ew4.sh build          # 编译 + sync mods → web & apk
scripts/ew4.sh install        # publish → install/web/app + sync
scripts/ew4.sh sync-mods      # 或 sync-mods web | apk | all
scripts/ew4.sh web            # --home install/web
scripts/ew4.sh apk            # Android 壳 → install/apk/ew4.apk（见 task/plan/android-shell.md）
```

## 代码中的路径

| 程序集 | 类型 | 含义 |
|--------|------|------|
| `Ew4.Platform.Persistence` | `Layout.InstallLayout` | 渠道安装根下 `app`、`mods`、`user`、workspaces、pending-imports |
| `Ew4.Platform.Persistence` | `Layout.RepositoryPaths` | 仓库根相对路径（`maps/`、`data/import-seed`、`wwwroot` 开发路径等） |
| `Ew4.Platform.Persistence` | `Layout.InstallChannelProbe` | 从 CWD / 进程路径推断 `EW4_HOME` |
| `Ew4.Service` | `Host/WebHostPaths` | publish 的 `wwwroot` 与开发态 `src/Ew4.Front.Web/wwwroot` |

解析安装根：`--home` → `EW4_HOME` → `<repo>/install/web`（`InstallLayout.DefaultUnderRepository`）。  
解析仓库根：`--root` → `EW4_REPOSITORY_ROOT` → 当前目录（若在仓库根且存在 `Ew4.sln`）。
