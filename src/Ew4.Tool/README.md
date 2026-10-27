# Ew4.Tool

离线 CLI；日常通过 `scripts/ew4.sh` 调用。

## 分层

| 层 | 位置 | 用途 |
|----|------|------|
| **cli/** | `Ew4.Tool.Cli` | 命令行：解析参数、打印结果、`--help` |
| **lib/** | `Ew4.Tool.Lib.Export` / `Ew4.Tool.Lib.Import` | APK 导出、APK/seed 导入；**不**放在 `Ew4.Platform.Content` |
| **Platform**（引用） | Content / Mod / Persistence 等 | 合并 Mod、校验、剧本 btl 等运行时内容 API |
| **Service**（引用） | `Ew4.Service` | 仅 `smoke-start` 等 CLI 烟测 |

Web `POST /api/export-apk` 在 `ExportApkSteps` 内通过 `ExportApkToolInvoker` 子进程执行 `Ew4.Tool export-apk --json`（Service 不引用 Tool 程序集，避免依赖环）。

**隔离**：`src/` 内除 `Ew4.Tool` 外不得引用本程序集；单测放在 `tests/Ew4.Test.Tool`（见 `ToolReferenceIsolationTests`）。

路径对称：`Ew4.Platform.Mod.Apk.ModApkPathMapping`（APK → Mod）；`Ew4.Tool.Lib.Export.ApkAssetPathMapping`（Mod → APK）。

## 常用命令

见 `Ew4.Tool --help` 或 `scripts/ew4.sh help`。
