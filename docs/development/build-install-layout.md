# build/ 与 install/ 布局

## 目录分工

| 路径 | 作用 | Git |
|------|------|-----|
| `build/<项目名>/` | MSBuild 的 bin/obj（`Directory.Build.props`） | 否 |
| `install/` | 可运行安装根（`EW4_HOME`） | 否 |
| `install/app/` | `dotnet publish` 的 Web 宿主 + `wwwroot` | 否 |
| `install/mods/` | 内容包（`base` 等） | 否 |
| `install/user/` | `active.json`、存档、编辑器草稿、缓存 | 否 |

仓库根 **`mods/`** 为内容包维护目录（不进 Git，仅 `mods/README.md` 进 Git）。`scripts/ew4.sh build`、`install`、`web` 会把 `mods/` 拷贝到 **`install/mods/`**；运行时 `EW4_HOME` 只读后者。

## 命令

```bash
scripts/ew4.sh build          # 编译（build/）+ 拷贝 mods/ → install/mods/
scripts/ew4.sh install        # 发布到 install/app，并拷贝 mods/ → install/mods/
scripts/ew4.sh sync-mods      # 仅拷贝 mods/ → install/mods/
scripts/ew4.sh web            # --root <repo> --home install/
```

## 代码中的路径（每包一个文件）

| 程序集 | 文件 | 含义 |
|--------|------|------|
| `Ew4.Platform.Content` | `Layout/RepositoryPaths.cs` | 仓库根相对路径（`repo.build`、`repo.data.import_seed`…） |
| `Ew4.Platform.Runtime` | `Layout/InstallLayout.cs` | 安装根相对路径（`install.app`、`install.user.saves`…） |
| `Ew4.Platform.Runtime.Web` | `Layout/WebPaths.cs` | 发布态 `app/wwwroot` 与开发态 `repo.src.host.web.wwwroot` |

解析安装根：`--home` → 环境变量 `EW4_HOME` → `<repo>/install`。  
解析仓库根：`--root` → `EW4_REPOSITORY_ROOT` → 当前目录（若在仓库根且存在 `Ew4.sln`）。

`RepositoryLayout` 仅为兼容旧调用，新代码请直接用上述类型。
