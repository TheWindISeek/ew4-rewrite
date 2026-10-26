# tests

单元测试程序集命名与 **`src/` 目标项目** 对应（见 [docs/arch/module-responsibilities.md](../docs/arch/module-responsibilities.md)）：

| 测试项目 | 主要被测 `src` | 说明 |
|----------|----------------|------|
| `Ew4.Test.Platform.Engine` | `Ew4.Platform.Engine` | 已有（仅依赖 Engine，无 IO） |
| `Ew4.Test.Platform.Content` | `Ew4.Platform.Content` | 已有 |
| `Ew4.Test.Platform.Mod` | `Ew4.Platform.Mod` | `ModInstallGuard` 等 |
| `Ew4.Test.Platform.Persistence` | `Ew4.Platform.Persistence` | 槽位序列化（`SlotSaveEnvelope` 等） |
| `Ew4.Test.Platform.Play` | `Ew4.Platform.Play`（Match） | 已有 |
| `Ew4.Test.Service` | `Ew4.Service` | 已有（`ProfileStore`；不依赖游戏资源） |
| `Ew4.Test.Editor` | `Ew4.Platform.Editor` | 已有 |
| `Ew4.Test.Architecture` | 依赖图 | 锁定 `docs/arch/frozen/layering.md`；改白名单=改架构 |
| `Ew4.Test.Tool` | `Ew4.Tool` | 尚未创建 |
| `Ew4.Test.Interface` | `Ew4.Interface` | 已有 |

前缀 **`Ew4.Test.`** 表示测试程序集。`src` 改名时更新 csproj 的 `ProjectReference` 路径即可。

运行：

```bash
scripts/ew4.sh test
scripts/ew4.sh dotnet test tests/Ew4.Test.Platform.Content/Ew4.Test.Platform.Content.csproj
```

---

## Ew4.Test.Service → Service

| 测试类 | 验证什么 |
|--------|----------|
| `ProfileStoreTests` | 持久账户的内存工作副本与提交点：局内变化不写盘；提交/手动存档写盘；离开战役(1)与征服(2)提交，其他模式丢弃；无变化不重写；非法账户不改变状态 |

---

## Ew4.Test.Platform.Content → Content / Mod

内容包、合并视图、扩展 manifest、规则加载策略。

| 测试类 | 用例 | 验证什么 |
|--------|------|----------|
| `ExtensionScriptPolicyTests` | `Api2_rejects_manifest_scripts_list` | API 2 manifest 带 `scripts`（Lua）时拒绝 |
| | `Api1_loads_legacy_lua_handlers` | API 1 仍允许 Lua handler（过渡） |
| `ModMergeTests` / `ModSnapshotTests` | … | parent 合并、`IModSnapshot` 指针索引 |

---

## Ew4.Test.Platform.Mod → Mod

| 测试类 | 说明 |
|--------|------|
| `ModInstallGuardTests` | 安装/激活/删除 parent 链、格式与 base 保护 |

---

## Ew4.Test.Platform.Persistence

局内存读档信封（`Ew4.Platform.Persistence.Serialization`）。

| 测试类 | 用例 | 验证什么 |
|--------|------|----------|
| `SlotSaveEnvelopeTests` | `Wrap_unwrap_roundtrip_and_rejects_mismatch` | 信封与 pack/指纹绑定；指纹不符拒绝 |
| | `Unwrap_accepts_legacy_slot_without_envelope` | 无信封的旧 slot JSON 仍可读 |

---

## Ew4.Test.Editor → `Ew4.Platform.Editor`

编辑器工作区、剧本 BTL ↔ JSON、patch。

| 测试类 | 用例 | 验证什么 |
|--------|------|----------|
| `WorkspaceServiceTests` | `Create_overlay_workspace_lists_and_reads_parent_file` | 创建 overlay 工作区、列表、读 parent 文件 |
| `ScenarioEditToolkitTests` | BTL/JSON roundtrip | 语料存在时字节一致 |
| `ScenarioEditPatchTests` | army cell patch | patch 与 roundtrip |

依赖 `mods/base/source/original/*.btl` 的用例在语料缺失时 **跳过**。

---

## Ew4.Test.Platform.Engine → Engine

纯规则单测，项目只引用 `Ew4.Platform.Engine`。

| 测试类 | 验证什么 |
|--------|----------|
| `HexMapTests` | 坐标/邻接/距离/范围 |
| `Ew4RandomTests` | 确定性序列（存档回放依赖，锁定首值） |
| `VictoryTests` | 胜负判定、回合上限、目标数 |
| `EconomyAndProgressionTests` | 收入/缺粮等级/资源上限、经验晋升 |
| `HazardAndSurvivalTests` | 灼烧伤害与衰减、势力存活判定 |
| `RuleSetValidationTests` | 规则配置只在 `RuleSet.Validate()` 校验一次 |

---

## 计划增补

| 测试项目 | 预期覆盖 |
|----------|----------|
| `Ew4.Test.Platform.Engine` | `IEngine` / 规则 |
| `Ew4.Test.Service` | `IGameOperations`、Play/Mod Invoke |
| `Ew4.Test.Tool` | CLI 子命令 |
| `Ew4.Test.Architecture` | 解决方案依赖图 |

门禁流程：[docs/task/todo.md](../docs/task/todo.md)（G1–G6）。
