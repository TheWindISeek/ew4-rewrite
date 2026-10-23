# Ew4.Tool

离线 CLI；日常通过 `scripts/ew4.sh` 调用。

## 分层

| 层 | 位置 | 用途 |
|----|------|------|
| **Content**（引用） | `Ew4.Platform.Content` | 运行时 Pack 解析、校验、`ScenarioBtlResolver`、`ScenarioBtlCodec` 等 |
| **Tool 导入** | `Ew4.Tool.Import` | `import-base`、APK→差异包、剧本嵌入/校验、国旗占位；**不**作为 Content 对外 API |
| **CLI** | `Program.cs`、`ApkImportCli.cs`、`ScenarioCli.cs` … | 命令行入口 |

需要 APK 转换能力时：引用 `Ew4.Tool` 项目或调用 `Ew4.Tool.Import.ApkOverlayPackImporter.Import`，不要从 Content 拉取 APK 逻辑。

## 常用命令

见 `Ew4.Tool --help` 或 `scripts/ew4.sh help`。
