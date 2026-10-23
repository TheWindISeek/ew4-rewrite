# install/ — 安装根（EW4_HOME）

与源码树并列的运行时目录，**不进 Git**。

| 路径 | 说明 |
|------|------|
| `app/` | `scripts/ew4.sh install` 发布的 Web 宿主与 `wwwroot` |
| `mods/` | 内容包（由仓库根 **`mods/`** 拷贝同步；勿在此直接维护） |
| `user/` | `active.json`、`saves/`、`workspaces/`、`cache/`、`logs/`、`tmp/` |

生成方式：

```bash
scripts/ew4.sh install    # publish + 同步仓库 mods/
scripts/ew4.sh web        # 开发态运行（默认 --home install/）
```

环境变量：`EW4_HOME`（默认 `<仓库>/install`）。详见 [build-install-layout.md](../docs/development/build-install-layout.md)。
