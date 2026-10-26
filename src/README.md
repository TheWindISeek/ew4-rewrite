# src

新代码树。设计见 [docs/](../docs/README.md)，任务与进度见 [docs/task/todo.md](../docs/task/todo.md)。

## 分层（程序集名）

```text
Ew4.Front.Web          表现层（静态 UI）
Ew4.Backend.Web        接入层（Kestrel、/api/*、DI；仅 Interface + Service）
Ew4.Service            编排层（IGameOperations、Play、Session、`ModOps` 桥接）
Ew4.Platform.*         平台层（Persistence → Mod → Content → Engine；Play、Editor）
Ew4.Tool               离线 CLI
Ew4.Interface          跨层契约（Operations 词典、Play/Editor DTO）
```

| 项目 | 说明 |
|------|------|
| `Ew4.Platform.Persistence` | 磁盘边界：包 IO、槽位、`InstallLayout`、`RepositoryPaths`、`Io` / `Workspaces` |
| `Ew4.Platform.Mod` | `Activation/`、`Catalog/`、`Import/`、`Merge/`、`Snapshot/` |
| `Ew4.Platform.Content` | `CatalogLoad`、`StaticData`、`Campaign`、校验与 `ModSnapshotLoader` |
| `Ew4.Platform.Engine` | 规则引擎；`IEngine` / `RuleSet` |
| `Ew4.Platform.Play` | 游玩域：`Match/`（FlowQueue、MatchFlow、UndoSlot） |
| `Ew4.Platform.Editor` | 创作 workspace / campaign / commander |
| `Ew4.Service` | `GameOperationExecutor`、`PlayHost`、`Session/`、`Host/`（`ModOps`） |
| `Ew4.Backend.Web` | 进程入口；引用 `Service`、`Interface` |
| `Ew4.Front.Web` | `wwwroot`；不引用 `Service` |
| `Ew4.Tool` | import / validate / scenario / workspace；**`Ew4.Tool.Import`** |
| `Ew4.Interface` | `IGameOperations`、路由与分类词典 |

`Ew4.Platform.Engine/AssemblyInfo.cs`：`InternalsVisibleTo` 含 `Ew4.Test.Platform.Engine`（项目待建）。
