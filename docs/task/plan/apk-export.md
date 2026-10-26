# APK 导出（Mod → 可安装包）

状态:**计划中（未实现）；`Ew4.Tool.Export` 里只有空骨架（接口已留好，实现由维护者完成）**  
关联：[roadmap.md](./roadmap.md) H3；对称工具 `import-apk-overlay`（`Ew4.Tool`）。

## 1. 背景

- Rewrite 游玩与编辑使用 **`mods/<id>/`** 或 **`.ew4mod`**；已有 **`workspace export`** 与 **`import-apk-overlay`**（APK → 差异包目录）。
- 作者与玩家需要：**在保留原版安卓壳（dex/so/启动逻辑）的前提下**，把合并后的 Mod 内容写回 APK，侧载安装即可玩，**不必**再携带 `ww1.apk` 等第三方 Mod APK。
- 发行物内置 **唯一壳模板** `ew4-base.apk`（完整原版包）；`data/import-apk/*.apk` 仅维护者导入用，**不进用户导出链路**。

## 2. 目标

| ID | 需求 | 说明 |
|----|------|------|
| R1 | **单壳模板** | 导出始终复制内置/指定的 **`ew4-base.apk`**，只改内容与 Manifest 元数据，不重写 Java 玩法。 |
| R2 | **Mod 合并** | 输入为已安装 mod 链或 workspace/`.ew4mod`；合并语义与运行时 **`MergedModSource`** / editor **`standalone`** 一致。 |
| R3 | **路径对称** | Mod 逻辑路径 → APK `assets/…` 使用与 **`ApkOverlayModImporter.ResolveModPath`** 对称的逆映射。 |
| R4 | **剧本格式** | 变更的 `scenarios/*.json`（schema 3）导出前转为原版可读的 **btl** 并写入 APK 对应路径。 |
| R5 | **用户另存为** | CLI **`--out`** 指定输出 APK 路径；后续 UI 等同「选保存路径」。 |
| R6 | **导出唯一性** | 每次导出用 **Unix 时间戳（秒）** 作为 `exportId`；写 `versionCode`、`versionName`、**带后缀的包名**，使不同用户/多次导出为 **不同应用**（可并存）。同一秒内碰撞 **不处理**。 |
| R7 | **统一签名** | 使用项目配置的 keystore **`apksigner`** 签名；用户只拿成品 APK。 |
| R8 | **Tool 归属** | 实现放在 **`Ew4.Tool`**，首入口为 **CLI**；不进入 `Ew4.Service` 热路径。 |

## 3. 非目标（首期）

- **覆盖安装**：固定包名、仅升 `versionCode` 的「第二次导出替换第一次」——暂缓。
- **把 `Ew4.Backend.Web` publish 打进原版壳**：属于 WebView + .NET 产品线，与本方案无关。
- **iOS 侧载 APK**：不做。
- **在发行 zip 中捆绑** `ww1.apk` / `huangkai.apk` 等：仅 `mods/` 目录与内置壳。

## 4. 内置与维护者资产

| 路径 | 用途 | 用户导出是否必需 |
|------|------|------------------|
| `data/shell/ew4-base.apk`（建议落盘；当前样例 `~/Downloads/ew4-base.apk`） | 唯一壳模板 | 否（CLI 默认可解析仓库内路径） |
| `data/import-apk/*.apk` | `import-apk-overlay` 生成 `mods/ww1` 等 | 否 |
| `mods/base`、`mods/<id>/` | 合并内容来源 | 是（或 `.ew4mod` / workspace） |

壳文件建议记录 **SHA256**（实现时校验，防止误换包）。

## 5. 用户流程（产品）

1. 在编辑环境完成 Mod（或激活 `base` + `ww1` + overlay）。
2. 触发 **导出 APK**（首期：命令行；后续：编辑器按钮）。
3. 选择 **输出路径**（如 `my-mod-1730000000.apk`）。
4. 工具：合并 → 逆映射 → btl → 补丁 `assets/` → 改 Manifest → zipalign → 签名。
5. 用户将 APK 安装到设备，**无需**再导入 zip 或其它 Mod APK。

## 6. 合并与 parent

- 支持基于 **`base`** 的差异包，以及 **`mods/ww1`** 等已安装包（合并视图，**不**再读 ww1 的 APK）。
- 运行时 **parent 仅一层**（parent 不能再有 parent）。导出时以 **最终合并字节** 为准；若在 ww1 上还有 overlay，应使用 **standalone 合并结果** 或显式 `--mod` 链（实现文档化一种主路径）。
- 相对 **`mods/base`**（或工具指定的 `--base`）**无差异**的文件 **不写入** APK 补丁（减小体积），除非产品 later 选择全量 `assets/` 替换。

## 7. 唯一性约定（`exportId`）

- **`exportId`**：`int`，UTC **Unix 秒**；CLI 默认 `DateTimeOffset.UtcNow.ToUnixTimeSeconds()`，可用 `--export-id` 覆盖（测试用）。
- **`versionCode`**：等于 `exportId`。
- **`versionName`**：`{modId}-{exportId}` 或 `{displayName}-{exportId}`（实现时二选一并写死）。
- **`applicationId`**：在原壳包名后追加后缀，例如 `{basePackage}.mod.{modId}.e{exportId}`（段名符合 Android 规则；以 `ew4-base.apk` 解压 Manifest 为准）。
- **可选戳记**：写入 APK `assets/` 下仅发行用文件（如 `assets/client/ew4-export.json`），字段含 `exportId`、`modId`、时间；原版不读取即可。

## 8. 对外接口 — CLI（规范）

**程序集**：`Ew4.Tool`  
**命令名**：`export-apk`  
**注册**：`Program.cs` switch + `PrintHelp()`；可选 `scripts/ew4.sh export-apk` 转发。

### 8.1 用法摘要

```text
Ew4.Tool export-apk --out <path.apk>
  [--shell <ew4-base.apk>]
  [--mods <install/mods>] [--base <mods/base>]
  (--active <modId> | --mod <modId> [--with-parent])
  [--workspace <dir> | --ew4mod <file>]
  [--export-id <unix-seconds>]
  [--stamp]   # 默认 true：写入 ew4-export.json
  [--sign --keystore <path> --ks-pass <env:VAR> ...]   # 未实现签名时可先输出 unsigned
```

### 8.2 参数

| 参数 | 必填 | 默认 | 说明 |
|------|------|------|------|
| `--out` | 是 | — | 输出 APK 绝对或相对路径。 |
| `--shell` | 否 | `data/shell/ew4-base.apk`（相对仓库根） | 壳模板 APK。 |
| `--mods` | 否 | `install/web/mods` 或 `mods` | 已安装 mod 根目录（`FileModRepository`）。 |
| `--base` | 否 | `mods/base` | 合并基线；用于 diff「是否写入 APK」。 |
| `--active` | 与 `--mod` 二选一 | — | 读取 `user/active.json` 对应 mod 并解析 parent。 |
| `--mod` | 与 `--active` 二选一 | — | 指定主 mod id；若 manifest 有 `parent`，自动打开 parent 合并（一层）。 |
| `--workspace` | 否 | — | 使用 workspace 目录作为 overlay 源（与 editor 磁盘布局一致）。 |
| `--ew4mod` | 否 | — | 解压或只读 zip 根含 `mod.json` 的包作为 overlay。 |
| `--export-id` | 否 | 当前 Unix 秒 | 见 §7。 |
| `--stamp` | 否 | true | 是否写入 `ew4-export.json`。 |
| 签名相关 | 否 | 读配置/环境变量 | `zipalign` + `apksigner`；具体变量名实现时在本文 §8.3 补全。 |

**冲突**：`--workspace` / `--ew4mod` 与 `--active`/`--mod` 同时出现时，以 **workspace / ew4mod 为 overlay**，parent 仍由 `--mod` 或 workspace 元数据决定（实现时择一规则并测试）。

### 8.3 退出码与输出

| 码 | 含义 |
|----|------|
| 0 | 成功；stdout 一行摘要：`export-apk: out=… exportId=… filesPatched=…` |
| 2 | 参数错误 |
| 其他 | 内容/映射/IO/签名失败；stderr 可读消息 |

### 8.4 实现模块（代码布局）

| 组件 | 建议路径 |
|------|----------|
| 导出核心 | `Ew4.Tool.Export.ApkOverlayModExporter`（或 `Import/` 下对称命名） |
| CLI | `Ew4.Tool.ExportApkCli` |
| 逆映射 | `ApkAssetPathMapping`（与 `ApkOverlayModImporter.ResolveModPath` 同文件或并列） |
| Manifest 补丁 | `ApkManifestPatch`（versionCode / versionName / package） |
| 签名 | `ApkSignerRunner`（调用宿主 `zipalign`/`apksigner`，或文档要求 PATH） |

**依赖**：`Ew4.Platform.Mod`（合并）、`Ew4.Platform.Persistence`（读包）、`Ew4.Tool.Import`（对照）、Scenario btl 转换（与 `ScenarioCli` / `ModScenarioConvert` 共用）。

## 9. 对外接口 — 未来 HTTP（非首期）

编辑器「导出 APK」可后续增加：

```http
POST /api/editor/export-apk
Content-Type: application/json

{ "workspaceId": "…", "outPath": "…" }   // 或返回 application/vnd.android.package-archive 流
```

- 实现应 **调用与 CLI 相同的 Exporter**，避免重复逻辑。
- 大文件与签名密钥 **仅服务端/本机 Tool**，不把 keystore 下发给浏览器。

首期 **不实现** HTTP；本文仅预留契约。

## 10. 与现有命令对照

| 命令 | 方向 |
|------|------|
| `import-apk-overlay` | APK → `mods/<id>/` |
| **`export-apk`** | 合并 Mod → APK |
| `workspace export` | workspace → `.ew4mod` |
| `POST /api/mod-import` | zip → `install/.../mods`（Rewrite 游玩） |

## 11. 验收标准

1. 给定 `mods/base` + 仅改 `def_commander.xml` 的 overlay，导出 APK 安装后将领改动在原版壳中可见。
2. 激活 `ww1`（parent `base`）导出，**不需要** `ww1.apk` 文件；内容与 Rewrite 激活 ww1 后一致（抽 1 关 + 1 张表 smoke）。
3. 连续两次导出，`applicationId` 不同（时间戳不同），设备上可同时安装（或至少第二次不报「签名冲突但包名相同」类错误）。
4. `exportId` 相同且手动指定时，行为可复现（测试用）。
5. CLI `--help` / `Ew4.Tool` 无参帮助列出 `export-apk`。
6. 文档：`Ew4.Tool/README.md` 与本文件一致。

## 12. 风险与依赖

- 壳版本与 `mods/base` **assets 布局漂移** → 映射回归。
- 二进制 **AndroidManifest** 编辑错误 → 安装失败；需真机或 emulator 验证。
- 宿主需 **Android build-tools**（`zipalign`、`apksigner`）或在 CI 镜像中预装。
- 再分发 `ew4-base.apk` 的合规由项目方负责。

---

实现启动后：在 [roadmap.md](./roadmap.md) 将 H3 标为进行中；合并后在 [done/](../done/) 摘要，并更新 `Ew4.Tool/README.md`。

---

## 13. 兼容性检查与 `--allow-lossy`（补充）

- 导出前先做**兼容性检查**，每项含：文件、字段、原因、丢弃后的影响。
- 默认遇到不兼容就**退出**（码 3）并打印清单；加 `--allow-lossy` 才继续，不兼容部分**静默丢弃**。
- 原版不认识的内容（多语言文件 `client/lang/`、BGM 配置 `client/music*`）属于"自动忽略"，不进清单。
- 不修改原 APK 的语言：原版有什么语言就是什么语言。

## 14. 前端接口（已留，未实现）

Plan 请求 `platform.export-apk`（HTTP `POST /api/export-apk`），当前恒返回 play 接口一贯的失败信封（HTTP 200，`ok:false`）：

```json
// 请求
{ "modId": "my-mod", "dryRun": true, "allowLossy": false }
// 响应（实现后）
{ "ok": true, "data": { "compat": [ { "path": "...", "field": "...", "reason": "...", "impact": "..." } ],
                        "outPath": "...", "exportId": 1730000000 } }
// 当前
{ "ok": false, "code": "export.unimplemented", "message": "APK export is not implemented yet." }
```

骨架类（均在 `Ew4.Tool.Export`）：`ApkExportRequest` / `ApkExportResult` / `ApkCompatIssue`、`IApkExporter`、`ApkOverlayModExporter`（`NotImplementedException`）、`ExportApkCli`（CLI 占位，退出码 2）、`ApkCompatibilityChecker`（同）。
