# mods/ — 内容包（运行时数据，不进 Git）

每个子目录是一个 **Pack**（含 `pack.json`）：

| 目录 | 说明 |
|------|------|
| **`base/`** | 原版；宿主默认激活 |
| **`<id>/`** | 其它 Mod（与 `base` 同级新增即可） |

存档与当前启用包 ID 在 **`install/user/`**（`EW4_HOME`，默认 `install/`）。

安装 / 导入 Mod：通过 **`Ew4.Backend.Web`** 的 HTTP API（如 `/api/mod-import`），不要依赖 shell 脚本。

策划向 JSON（地图等）在 **`maps/`**，不放在 `mods/`。

维护：从原料重建 `base` 用 `scripts/ew4.sh import-base`（`data/import-seed` → `mods/base`）；APK 差异包用 `Ew4.Tool import-apk-overlay`（落盘 `mods/<id>/`）。

`scripts/ew4.sh build`、`install`、`web` 会把 **`mods/` 同步到 `install/mods/`**（运行时 `EW4_HOME` 读后者）。
