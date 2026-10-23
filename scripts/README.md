# scripts

宿主机**只装 Docker**；编译、测试、CLI、Web 均通过 **`ew4.sh`** 在容器内执行 `dotnet`。

## 入口

| 文件 | 作用 |
|------|------|
| **[ew4.sh](ew4.sh)** | 唯一常用入口：`help` 子命令列表见文件头注释 |
| [windows/](windows/) | Windows 快捷方式（打开浏览器等），非构建链路 |

## 内部模块（`dev/`）

不要直接 `bash dev/xxx.sh`；由 `ew4.sh` 按顺序 `source`。

| 文件 | 职责 |
|------|------|
| [dev/paths.sh](dev/paths.sh) | 仓库根、`install/web` & `install/apk`、`EW4_HOME` / `EW4_CHANNEL`、mods 同步、容器 `/src` 路径 |
| [dev/docker.sh](dev/docker.sh) | `ew4-dev:local` 镜像、`docker run` 里跑 dotnet |
| [dev/cli.sh](dev/cli.sh) | 包装 **Ew4.Tool**：`import-base`、`validate`、`smoke-start`、`check btl` |
| [dev/install.sh](dev/install.sh) | `publish` → `install/web/app/`，并同步 `mods/` → web & apk |

## 常用命令

```bash
scripts/ew4.sh build              # 编译后 sync mods → install/web & install/apk
scripts/ew4.sh sync-mods          # 默认 all；或 sync-mods web | apk
scripts/ew4.sh install          # publish → install/web/app
scripts/ew4.sh web                # http://127.0.0.1:5184（--home install/web）
scripts/ew4.sh test
scripts/ew4.sh import-base        # 需本地 data/import-seed
scripts/ew4.sh validate smoke
```

内容包在仓库 **`mods/`** 维护；Web 运行时读 **`install/web/mods/`**，APK 读 **`install/apk/mods/`**。详见 [build-install-layout.md](../docs/development/build-install-layout.md)。
