# 内容、Mod 与数据规则（冻结）

## 内容包

1. **玩法与内容在 `mods/<packId>/`，引擎与宿主在 `src/`。** 内容变化不应要求改引擎代码。
2. `mods/base` 是原版内容包（本地维护，二进制数据含地形，不进 Git）。其它 Mod 用 `parent: "base"` 按**文件路径**与 base 合并；合并语义只在 `Ew4.Platform.Mod`。
3. **核心地形/地图二进制由维护者提供**，Mod 作者不设计新地图；Mod 只改数值、战役、将领、资源等。引入新地图/资源目录需要先立项，不是顺手加。
4. 两个安装渠道**互相隔离**：`install/web/`（`app/`、`mods/`、`user/`）与 `install/apk/`。作者维护在仓库 `mods/`，经 `sync-mods` 同步到两侧；**不手改 install 下的 mods**。
5. 激活状态、存档、用户设置只在渠道的 `user/` 下，由 Persistence 读写。

## 存档与校验

- 存档带 `sha256` 完整性字段；指纹/revision 用 SHA-256。**一律通过 `Ew4.Interface.Common.Sha256Hash`**，不得直接用 `SHA256.*`（Android 无 OpenSSL，系统实现会让 Mono 直接 abort，捕获不了）。
- 托管与系统实现输出必须逐字节一致，存档才能跨平台。
- 存档格式只增不破：改 schema 要写迁移/兼容读取。

## 多语言

- 语言是**内容包的数据**：`assets/client/lang/<code>.json`（`zh-Hans` 默认、`zh-Hant`、`en`），不是代码。
- 回退链固定：当前语言覆盖表 → zh-Hans 覆盖表 → 原版字符串表 → key 本身。缺翻译不报错。
- 界面文字用 `ui.` 前缀 key；前端 `ew4t(key, fallbackZh, args)` 与 `data-i18n`。
- 地名双语字段（`cityLanguage` 等）独立设置，不并入此机制。
- 不修改原版 APK 的语言；导出时语言文件属于“原版不认识、忽略”的内容。

## Mod 兼容与导出

- 导入/导出遇到不兼容内容：**警告，用户选择继续则静默丢弃**，不中断、不猜测修复。
- 导出 APK：只在 `Ew4.Tool` 与前端留接口，实现由维护者负责；AI 不得擅自实现 base-apk 改写。

## 不允许

- 内容规则写进代码（原版数值、AI 配方、技能参数应来自 mod）。
- 绕过 `Mod` 视图直接读 `mods/` 目录。
- 合并语义分散到多处实现。
