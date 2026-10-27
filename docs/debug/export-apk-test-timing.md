# APK 导出相关测试耗时（基线）

在 **`ew4-dev:local`** 容器内、`./scripts/ew4.sh build` 之后测量。用于区分「卡死」与「正常慢」（dry-run 会扫合并 Mod + shell 索引）。

测量命令示例：

```bash
./scripts/ew4.sh dotnet test tests/Ew4.Test.Tool/Ew4.Test.Tool.csproj -c Debug --logger "console;verbosity=detailed"
./scripts/ew4.sh dotnet test tests/Ew4.Test.Service/Ew4.Test.Service.csproj -c Debug --filter FullyQualifiedName~ApkExportStepsTests --logger "console;verbosity=detailed"
```

## 2026-10-27 基线（单次）

| 测试 | 耗时 | 说明 |
|------|------|------|
| `ApkAssetPathMappingTests.Shell_index_roundtrips_btl_file_name` | **9 ms** | 纯内存映射 |
| `ApkExportTests.DryRun_ex_commander_skills_patches_commander_table` | **~818 ms** | 库内 `ApkExporter.Export` dry-run |
| `ApkExportTests.DryRun_ex_conquest_tweak_patches_scenario_btl` | **~1 s** | 含 scenario→btl |
| `ApkExportTests.DryRun_huangkai_merged_overlay_has_asset_patches` | **~914 ms** | 大 overlay（mods/huangkai 存在时） |
| **Ew4.Test.Tool 合计** | **~3.4 s** | 4 个用例 |
| `ApkExportStepsTests.Check_route_returns_ok_for_ex_commander_skills` | **~1 s** | Service → `dotnet exec` 已构建的 `build/Ew4.Tool/Debug/net10.0/Ew4.Tool.dll` |
| `ToolReferenceIsolationTests`（2 条） | **&lt; 300 ms** | 扫描 csproj 引用 |

## 若明显超出基线

- **&gt; 60 s 无输出**：怀疑子进程未找到 Tool DLL（应先 `./scripts/ew4.sh build`），或误用 `dotnet run` 反复编译；Web 路径应优先 `EW4_TOOL_DLL` / `build/Ew4.Tool/.../Ew4.Tool.dll`。
- **&gt; 5 s 的单条 dry-run**：检查 `data/shell/ew4-base.apk` 是否在、mods 是否异常巨大。

## 架构

- `src/**` 除 `Ew4.Tool` 外不得 `ProjectReference` Tool（`ToolReferenceIsolationTests`）。
- 仅 `tests/Ew4.Test.Tool` 可引用 Tool 程序集；其它层通过 CLI 子进程调用。
