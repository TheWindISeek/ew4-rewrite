# Ew4.Tool

离线 CLI；日常通过 `scripts/ew4.sh` 调用。

## 分层

| 层 | 位置 | 用途 |
|----|------|------|
| **Content**（引用） | `Ew4.Platform.Content` | Pack 解析、校验、`ScenarioBtlResolver`、`ScenarioBtlCodec` 等 |
| **Service**（引用） | `Ew4.Service` | `smoke-start` 等使用 `CliEngineGame` |
| **Persistence**（引用） | `Ew4.Platform.Persistence` | 路径与包 IO（经 Tool 间接使用） |
| **Tool 导入** | `Ew4.Tool.Import` | `import-base`、APK→差异包、剧本嵌入/校验、国旗占位；**不**作为 Content 对外 API |
| **Tool 导出** | `Ew4.Tool.Export`（计划） | `export-apk`：合并 Mod → 壳 APK；见 [apk-export.md](../../docs/task/plan/apk-export.md) |
| **CLI** | `Program.cs`、`ApkImportCli.cs`、`ScenarioCli.cs` … | 命令行入口 |

需要 APK 转换能力时：引用 `Ew4.Tool` 项目或调用 `Ew4.Tool.Import.ApkOverlayModImporter.Import`，不要从 Content 拉取 APK 逻辑。

## 常用命令

见 `Ew4.Tool --help` 或 `scripts/ew4.sh help`。
