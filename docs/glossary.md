# 术语表

状态:已实现（与当前 `src/` 对齐；细节以代码为准）

## 仓库与运行时

| 术语 | 定义 |
|------|------|
| **安装根 (`EW4_HOME`)** | 渠道根，如 `<仓库>/install/web` 或 `install/apk`：`app/`、`mods/`、`user/` |
| **`mods/`（仓库根）** | 内容包**维护**目录（`base` + 其它 `<id>/`）；仅 `mods/README.md` 进 Git |
| **`install/web/mods/`** | **Web 运行时**内容包镜像（从 `mods/` 拷贝）；Host 读取此处 |
| **`mods/base`** | 原版 Pack；`import-base` 或本机维护 |
| **`install/mods/<id>/`** | 运行时副本；也可经 Web `/api/mod-import` 直接写入 install（与仓库 `mods/` 需自行对齐） |
| **`install/user/`** | `active.json`、存档、workspaces 等运行时数据，不进 Git |
| **`maps/`** | 策划向 JSON（进 Git），**不是**运行时 Pack 的一部分 |
| **`build/`** | MSBuild 的 `bin`/`obj` 输出 |
| **`backup/`** | 封存旧代码，只读对照，**不参与** `Ew4.sln` 构建 |

## 分层（`src/`）

| 术语 | 定义 |
|------|------|
| **Core** | `Ew4.Platform.Engine`：纯规则。输入 `WorldState` + `RuleSet` + 命令，输出新状态；不读文件、不知 Pack |
| **`IEngine` / `Engine`** | Core 对外门面：`NewGame`、`Apply`、查询 API |
| **`RuleSet`** | 一局所需的全部规则参数（移动、战斗、回合、招募等）的汇总对象 |
| **Persistence** | `Ew4.Platform.Persistence`：**唯一磁盘边界**（包 IO、槽位、`active.json`、`InstallLayout`、`RepositoryPaths`、workspace 磁盘 API） |
| **Mod（程序集）** | `Ew4.Platform.Mod`：`Merge/`（parent 补全）、`Snapshot/`（`IModSnapshot`、内容指针索引） |
| **Content** | `Ew4.Platform.Content`：在 Mod 视图之上解析、校验、指纹 → `ResolvedContent`；`CatalogLoad`、`StaticData`、`Campaign` |
| **`ResolvedContent`** | 校验后的不可变内容快照；实现 `IModSnapshot`；供 Builder / 游玩使用 |
| **`IScenarioBuilder`** | 由剧本 id 构建初始 `WorldState` + `RuleSet`（如 `ScenarioJsonBuilder`） |
| **CliEngineGame** | `Ew4.Service.CliEngineGame`：Tool/CLI 向 Catalog·Start·Apply·Save/Load；**Web 游玩**主路径为 `PlayHost` + `PlaySession` |
| **Service** | `Ew4.Service`：`IGameOperations` 编排；`Play/`、`Host/`、`CliEngineGame` |
| **Backend.Web** | `Ew4.Backend.Web`：Kestrel、DI、`/api/*`、发布 `Ew4.Front.Web` 静态资源 |
| **Front.Web** | `Ew4.Front.Web`：`wwwroot` 静态 UI |
| **Interface** | `Ew4.Interface`：跨层契约（`IGameOperations`、`Operations` 词典等） |
| **Host** | `Ew4.Service/Host`：`HostApp` 激活 Pack、workspace 预览 |
| **Play** | `Ew4.Platform.Play`：游玩域（一局内行动队列、撤销）；原名 Runtime |
| **Plan / Step** | 请求 → 底层原子操作的对应，见 `docs/arch/operation-plans.md` |
| **Editor** | `Ew4.Platform.Editor`：Workspace、战役/将领创作；HTTP 经 Service `Invoke` |

## 内容包（Pack）

| 术语 | 定义 |
|------|------|
| **Pack** | 含根清单 `mod.json` 的目录或 `.ew4mod`/`.zip`。原版与 Mod **同一种模型** |
| **`mod.json`** | 包唯一清单：`id`、`version`、`parent`、`requires`、`formats`，扩展段 `scenarios`（见 [mods/README.md](../mods/README.md)） |
| **Active Pack** | 当前激活的包 id，写在 `install/user/active.json` |
| **parent 补全** | Mod 声明 `parent: "base"` 时，缺路径的文件从父包整文件补齐（`Ew4.Platform.Mod.Merge`） |
| **指纹 (Fingerprint)** | 参与校验的源文件哈希汇总；存档绑定包 id + 指纹 + 引擎行为版本 |
| **ContentKind** | 注册表：某类文件的路径模式、schema、是否参与指纹 |
| **校验级别** | `Schema` → `Reference` → `Constraint` → **`SmokeStart`**（真 `NewGame` 试开局） |
| **运行时资源路径** | Pack 内 `assets/`、`source/` 等；Builder 通过 `ModAssetRead` 等映射旧路径 |

## 游玩与接口

| 术语 | 定义 |
|------|------|
| **`GameSession`** | Core 中的一局会话数据（含 `WorldState`、revision 等） |
| **Play API** | `POST /api/{route}?q=…`；由 Backend → `IGameOperations.Invoke` |
| **revision** | 局内状态版本号；客户端带 `revision` 防陈旧命令 |

- **Session（层） vs `GameSession`（类型）**：前者是服务与存档边界（Persistence 序列化 + Service 编排）；后者是 Core 里的状态记录。

架构细节见 [docs/README.md](README.md) 中的 **arch/** 索引。
