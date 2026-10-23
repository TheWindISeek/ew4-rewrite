# src

新代码树。设计见 [docs/](../docs/README.md)，任务与进度见 [docs/task/todo.md](../docs/task/todo.md)。

## 分层（程序集名）

```text
Ew4.Front.Web          表现层（静态 UI）
Ew4.Backend.Web        接入层（Kestrel、/api/*、DI）
Ew4.Service            服务层（游玩 / Mod 编排）
Ew4.Platform.*         平台层（Engine → Content → Session → Runtime）
Ew4.Editor             创作（HTTP / Tool 共用）
Ew4.Tool               离线 CLI
Ew4.Interface          跨层契约
```

| 项目 | 说明 |
|------|------|
| `Ew4.Platform.Engine` | 规则引擎；`IEngine` / `RuleSet` |
| `Ew4.Platform.Content` | Pack、校验、Builder、`Original` 导入 |
| `Ew4.Platform.Session` | `ISessionService`、存档 |
| `Ew4.Platform.Runtime` | `HostApp`、Pack 激活与 zip 导入 |
| `Ew4.Service` | 实现 `Interface.Front` / 服务编排 |
| `Ew4.Backend.Web` | 进程入口；引用 `Service`、`Editor`、`Platform.*` |
| `Ew4.Front.Web` | `wwwroot`；不引用 `Service` |
| `Ew4.Editor` | `IScenarioEditToolkit` |
| `Ew4.Tool` | import / validate / scenario / workspace；**`Ew4.Tool.Import`**（APK、`import-base`、剧本嵌入，非 Content 程序集） |
| `Ew4.Interface` | HTTP 与服务门面 DTO |

`Ew4.Platform.Engine/AssemblyInfo.cs`：`InternalsVisibleTo` 含 `Ew4.Test.Platform.Engine`（项目待建）。
