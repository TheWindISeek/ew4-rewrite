# Mod 统一视图（设计稿）

状态：**设计稿**（先定抽象，再改实现）  
关联：[mods/README.md](../../mods/README.md)（作者包目录）、[arch/README.md](./README.md)（分层与 HTTP）

---

## 1. 要解决什么问题

- **Mod 是核心数据单元**：玩法与外观来自 `install/<channel>/mods/<id>/`，不是来自 `src/` 里的 C#。
- **数据和逻辑要分开**：磁盘上的文件是数据；Merge、校验、指纹、从表生成 `catalog` 是逻辑。
- **使用期应简单**：游戏运行、HTTP、`/content` 只依赖一份**已就绪的 Mod 视图**，不在热路径上反复 Merge 或全包校验。

当前实现已部分符合（激活后持有 `ResolvedContent`），但读路径仍通过 `IPackSource` 在运行时做「子/父二选一」。本稿目标是把语义收成：**载入阶段生成快照；使用阶段只查快照（逻辑路径 → 物理指针）**。

---

## 2. 两阶段模型

```text
┌─────────────────────────────────────────────────────────────┐
│  A. 载入（生成）— 仅在安装 / 激活 / 显式重建时执行          │
│     安装 → 合并(parent) → 校验 → 指纹 → 构建 ModSnapshot   │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│  B. 使用 — 进程内游玩、/api、/content、Session、存档绑定    │
│     只读 ModSnapshot；用 revision 做对账，不重新 Merge/校验   │
└─────────────────────────────────────────────────────────────┘
```

| 阶段 | 何时触发 | 做什么 | 不做什么 |
|------|----------|--------|----------|
| **安装** | zip 导入、拷贝到 `mods/<id>/` | 落盘、`mod.json` 与 id 一致、可选安装前探测 | 不算指纹、不建游玩用缓存 |
| **激活载入** | 用户选 Mod、`HostApp.Activate`、创作预览 | Merge、校验、指纹、生成 **ModSnapshot**、写 `user/active.json` | 不启动局内逻辑 |
| **可选构建** | `Ew4.Tool import-base` 等 | 从种子/BTL **生成**包内文件 | 与玩家运行时激活无关 |
| **使用** | 每条 `/api/*`、`/content/*`、开局/存档 | `snapshot.Read(logicalPath)`、比对 `revision` | 不全包 Schema 扫描、不重新 Merge |

**指纹（revision）**：在 **激活载入** 结束时对「合并后的有效文件集」计算一次；使用期只携带与比较，不每次 Read 重算。

---

## 3. ModSnapshot（统一 Mod 视图）

使用期的唯一内容入口（名称可与现有 `ResolvedContent` 对齐或逐步替换）。

```text
ModSnapshot
├── Identity        激活的 mod id、manifest（name、version、parent、requires…）
├── Revision        内容指纹（fingerprint），供存档与 API 同步
└── Resolve         逻辑包内路径 → 字节（或流）
```

### 3.1 逻辑路径 vs 物理指针

作者与工具在包里用的是**包内相对路径**，例如：

- `assets/tables/def_commander.xml`
- `scenarios/campaign1_01.json`
- `assets/client/countries.json`

运行时还有一层 **封存路径**（`original/`、`data/`、`client/`），由 `PackAssetRead` 映射到包内路径（见 `Ew4.Platform.Content/Build/PackAssetRead.cs`）。  
**ModSnapshot 对上层应优先暴露「包内逻辑路径」或已映射的封存路径之一，并在文档中固定一种对外约定**；内部统一经 Resolve 完成。

**ContentRef（指针）** — 载入期为每个生效的逻辑路径记录「从哪读」，而不是在使用期再问 parent：

| 字段（概念） | 含义 |
|--------------|------|
| `LogicalPath` | 合并后的统一路径（子覆盖父后每个路径最多一条生效记录） |
| `Layer` | 来自 Primary（子 Mod）或 Parent（base） |
| `Physical` | 实际位置：已安装目录相对路径 / zip 条目 / 创作 workspace 路径等 |

使用期：`Read(logicalPath)` = 查表得 `ContentRef` → 读物理位置。  
**Merge 规则（载入期一次性）**：整文件级；同名路径子 Mod 覆盖父 Mod；仅存在于父的路径仍可用。

与今天代码的关系：`MergedPackSource` 是 **惰性** 的合并视图（读时再 primary→parent）；演进方向是载入期生成 **显式** 的 `LogicalPath → ContentRef` 索引（或保留惰性实现但对外只暴露 `ModSnapshot` API）。

---

## 4. 载入管线（激活）

建议顺序（与现 `HostApp.Activate` 一致，语义写清即可）：

```text
1. OpenPrimary(packId)           → 磁盘 DirectoryPackSource（或 workspace）
2. OpenParent(manifest.parent)   → 无 parent 则跳过
3. Merge(primary, parent)        → 有效文件集（视图）
4. Validate(view, level)       → 失败则拒绝激活，不写 active.json
5. Revision = Fingerprint(view)  → 哈希有效文件集
6. ModSnapshot = { Identity, Revision, Resolve/Index }
7. Persist active pack id        → user/active.json
8. Notify listeners              → PlaySessionHost 丢弃旧 ClientGame
```

校验范围（载入期）：

- 引擎版本、`mod.json` 契约、路径白名单、格式版本声明
- 可选更高级：reference / smoke（创作导出、CI）

导入 zip：**安装**单独一步；**激活载入**仍走上面 3–8（安装成功后再 `Activate(id)`）。

---

## 5. 使用期谁依赖 ModSnapshot

| 消费者 | 依赖方式 |
|--------|----------|
| `PlaySessionHost` / `ClientContent` / `ClientGame` | 当前激活 snapshot；`revision` 与请求上下文一致 |
| `PlayStaticFiles` `/content/*` | `Resolve` + 路径映射到 `client/`、`rewrite/` |
| `SessionService` / 存档 | 信封内写入 `packId` + `revision` |
| 前端 `wwwroot` | **不**直接读 snapshot；只通过 `/api/*` 与 `/content/*` |

前端程序（`Ew4.Front.Web/wwwroot`）不进 Mod；Mod 只提供 **静态资源** 与 **服务端算好的 JSON**（如 `/api/catalog`）。

---

## 6. 与现有类型的对照（迁移用）

| 设计概念 | 当前代码（约） | 备注 |
|----------|----------------|------|
| 安装 | `FilePackRepository.Install` | 仅落盘 |
| Merge | `PackMerge` / `MergedPackSource` | 载入期 |
| 载入 | `IModSnapshotLoader` / `ModSnapshotLoader` | Resolve + Validate |
| 校验 | `ContentValidator` + `PackInstallGuard` | 载入期 |
| 指纹 | `IModSnapshot.Revision`（= `Fingerprint`） | 载入期算，使用期带 |
| ModSnapshot | `IModSnapshot` ← `ResolvedContent` | 使用期读口 |
| 指针表 | `ContentRef` + `ModSnapshotIndex.Build` | 载入期可建索引 |
| 激活 | `HostApp.Activate` → `LoadActivatedView` | 生成 snapshot |
| 使用期读 | `ReadPackFile` / `TryReadRuntimeAsset` | 优先于直接 `Source.Read` |

创作 workspace 预览：视为 **另一条载入入口**（primary=workspace 目录，parent=声明的 base），产出同样是临时 ModSnapshot，不写 `active.json` 或写预览标记（见 `HostApp.EnterWorkspacePreview`）。

---

## 7. 实现路线与进度

| 步骤 | 状态 | 说明 |
|------|------|------|
| 文档与 `IModSnapshot` / `IModSnapshotLoader` | 已做 | `Ew4.Platform.Content/Mod/*`，测试 `ModSnapshotTests` |
| 激活走 `ModSnapshotLoader` | 已做 | `HostApp.Activate`；`ContentRefs` 在载入期构建 |
| Play 使用期读 snapshot | 已做 | `PlayEndpoints`、`PlayStaticFiles`、`PlaySessionHost`、`PlayClientFactory`、`ModCatalogService` |
| 创作/工具载入仍可直接 `ContentResolver` / `PackMerge` | 保留 | 载入阶段；`WorkspacePackSources`、`Ew4.Tool` |
| 使用期禁止 `PackMerge`（分析器或审查） | 未做 | 靠约定；Author 路径除外 |
| `SessionService` 等仍持 `ResolvedContent` | 过渡 | 与 `IModSnapshot` 同一实例 |

不必为「指针」过早做性能优化；差异 Mod 文件少时，底层仍可惰性 `MergedPackSource`，只要 **阶段边界** 和 **唯一读口** 清晰。

---

## 8. 原则摘要

1. **一个激活包 = 一个 ModSnapshot**（合并后的世界）。
2. **Merge / 校验只在载入生成 snapshot 时跑**。
3. **Revision 是使用期的身份**；换 Mod 必须换 revision，并刷新会话/前端。
4. **视图内是逻辑路径 + 物理指针**；业务代码不拼接 `install/.../mods/base/...`。
5. **前端代码在 wwwroot，Mod 是数据**；契约是 `/content` + `/api`。

后续实现讨论可在此文件下追加「决策记录」小节，或拆到 `docs/task/plan/` 的具体任务单。
