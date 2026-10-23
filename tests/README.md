# tests

单元测试程序集命名与 **重构完成后的 `src/` 目标项目名** 一一对应（见 [docs/architecture/11-simplification-and-layout.md](../docs/architecture/11-simplification-and-layout.md) §3.1）：

| 测试项目 | 目标被测 `src` | 说明 |
|----------|----------------|------|
| `Ew4.Test.Platform.Engine` | `Ew4.Platform.Engine` | 尚未创建 |
| `Ew4.Test.Platform.Content` | `Ew4.Platform.Content` | 已有 |
| `Ew4.Test.Platform.Session` | `Ew4.Platform.Session` | 已有 |
| `Ew4.Test.Platform.Runtime` | `Ew4.Platform.Runtime` | 尚未创建 |
| `Ew4.Test.Service` | `Ew4.Service` | 尚未创建 |
| `Ew4.Test.Editor` | `Ew4.Editor` | 已有 |
| `Ew4.Test.Tool` | `Ew4.Tool` | 尚未创建 |
| `Ew4.Test.Interface` | `Ew4.Interface` | 尚未创建 |

前缀 **`Ew4.Test.`** 表示测试程序集，避免与产品 DLL 混淆。`src` 改名时只改 csproj 的 `ProjectReference` 路径，**测试项目名不再跟着改一次**。

运行：

```bash
scripts/ew4.sh test
scripts/ew4.sh dotnet test tests/Ew4.Test.Platform.Content/Ew4.Test.Platform.Content.csproj
```

---

## Ew4.Test.Platform.Content → `Ew4.Platform.Content`

内容包、扩展 manifest、规则加载策略。

| 测试类 | 用例 | 验证什么 |
|--------|------|----------|
| `ExtensionScriptPolicyTests` | `Api2_rejects_manifest_scripts_list` | API 2 manifest 带 `scripts`（Lua）时拒绝 |
| | `Api1_loads_legacy_lua_handlers` | API 1 仍允许 Lua handler（过渡；架构计划删除 API 1） |

---

## Ew4.Test.Platform.Session → `Ew4.Platform.Session`

局内状态、存读档、`SlotSaveEnvelope`。

| 测试类 | 用例 | 验证什么 |
|--------|------|----------|
| `SlotSaveEnvelopeTests` | `Wrap_unwrap_roundtrip_and_rejects_mismatch` | 信封与 pack/指纹绑定；指纹不符拒绝 |
| | `Unpack_accepts_legacy_slot_without_envelope` | 无信封的旧 slot JSON 仍可读 |

---

## Ew4.Test.Editor → `Ew4.Editor`

编辑器工作区、剧本 BTL ↔ JSON、军队格子 patch（实现代码尚在 `Ew4.Editor`）。

| 测试类 | 用例 | 验证什么 |
|--------|------|----------|
| `WorkspaceServiceTests` | `Create_overlay_workspace_lists_and_reads_parent_file` | 创建 overlay 工作区、列表、读 parent 的 `pack.json` |
| `ScenarioEditToolkitTests` | `Json_roundtrip_preserves_export_bytes_when_btl_corpus_present` | 有 `campaign1_01.btl` 时 BTL→JSON→BTL 字节一致 |
| | `Validate_reports_all_checks_for_corpus_btl` | `ValidateBtlRoundtrip` 各项为真 |
| `ScenarioEditPatchTests` | `PatchArmyCell_updates_layer_and_armies_section` | patch 后逻辑层与 `armies` 段一致 |
| | `PatchArmyCell_preserves_btl_roundtrip_when_corpus_present` | patch 后导出仍与原文一致 |

依赖 `mods/base/source/original/*.btl` 的用例在语料缺失时 **跳过**（不失败）。

---

## 计划增补（目标名，尚未建项目）

| 测试项目 | 预期覆盖 |
|----------|----------|
| `Ew4.Test.Platform.Engine` | `IEngine` / 规则、与封存 Verify 对照 |
| `Ew4.Test.Platform.Runtime` | `InstallLayout`、包激活、zip 导入 |
| `Ew4.Test.Service` | 游玩与 Mod HTTP（`Interface.Front`） |
| `Ew4.Test.Tool` | CLI 子命令与 Platform 集成 |
| `Ew4.Test.Interface` | DTO 序列化、路由形状稳定性 |
| `Ew4.Test.Architecture` | 解决方案依赖图约束 |

门禁流程：[docs/development/verification.md](../docs/development/verification.md)。
