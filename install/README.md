# install/ — 多渠道运行时（不进 Git）

**最终发行形态：只拷贝本目录（含 `web/` 与可选 `apk/`）即可游玩**，无需仓库 `src/`、`Ew4.sln`。

## Web 游玩（最常见）

```text
install/
  web/
    app/          ← dotnet publish（Ew4.Backend.Web.dll + wwwroot）
    mods/         ← 至少 mods/base
    user/         ← 存档、active.json（可空目录）
    run.sh        ← install 后生成；或见下
```

```bash
# 方式 1：启动脚本（推荐，由 scripts/ew4.sh install 生成）
./install/web/run.sh

# 方式 2：手动（等价）
export EW4_HOME="$(pwd)/install/web"
cd install/web/app && dotnet Ew4.Backend.Web.dll --home "$EW4_HOME"
```

生成 `run.sh`：在仓库根执行 `scripts/ew4.sh install`。  
未 publish 时开发请用 `scripts/ew4.sh web`（依赖源码树）。

宿主会自动从 `app/` 或 `EW4_HOME` 推断安装根；静态页优先 `web/app/wwwroot`（publish 自带），不依赖 `src/`。

启动后打开 **http://127.0.0.1:5184**，左下角 **Mod** 可查看 `web/mods/` 下已安装包、**切换激活**（写入 `web/user/active.json`）或导入 zip / `.ew4mod`。与仓库开发时行为一致。

## 渠道目录

| 路径 | 说明 |
|------|------|
| **`install/web/`** | Web 渠道安装根（默认 `EW4_HOME`） |
| `install/web/app/` | 可执行宿主 + `wwwroot` |
| `install/web/mods/` | Web 内容包 |
| `install/web/user/` | Web 用户数据 |
| **`install/apk/`** | APK 渠道（`EW4_CHANNEL=apk`，与 Web 隔离） |
| `install/apk/mods/`、`install/apk/user/` | APK 内置资产与存档 |

维护者从仓库 `mods/` 同步：

```bash
scripts/ew4.sh sync-mods      # web + apk
scripts/ew4.sh install        # publish + sync + run.sh
```

环境变量：`EW4_HOME`、`EW4_CHANNEL`（`web` | `apk`）、`EW4_URLS`（`run.sh` 默认 `http://127.0.0.1:5184`）。

旧布局 `install/mods` 等会在 sync 时迁到 `install/web/`。

详见 [build-install-layout.md](../docs/development/build-install-layout.md)。
