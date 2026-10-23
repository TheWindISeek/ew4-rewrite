# 术语表

状态:已实现（与当前 `src/` 对齐；细节以代码为准）

## 仓库与运行时

| 术语 | 定义 |
|------|------|
| **安装根 (`EW4_HOME`)** | 默认 `<仓库>/install`：`app/`、`mods/`、`user/` |
| **`mods/`（仓库根）** | 内容包**维护**目录（`base` + 其它 `<id>/`）；仅 `mods/README.md` 进 Git |
| **`install/mods/`** | **运行时**内容包镜像（从 `mods/` 拷贝）；Web/Host 读取此处 |
| **`mods/base`** | 原版 Pack；`import-base` 或本机维护 |
| **`install/mods/<id>/`** | 运行时副本；也可经 Web `/api/mod-import` 直接写入 install（与仓库 `mods/` 需自行对齐） |
| **`install/user/`** | `active.json`、存档等运行时数据，不进 Git |
| **`maps/`** | 策划向 JSON（进 Git），**不是**运行时 Pack 的一部分 |
| **`build/`** | MSBuild 的 `bin`/`obj` 输出 |
| **`backup/`** | 封存旧代码，只读对照，**不参与** `Ew4.sln` 构建 |

## 分层（`src/`）

| 术语 | 定义 |
|------|------|
| **Core** | `Ew4.Platform.Engine`：纯规则。输入 `WorldState` + `RuleSet` + 命令，输出新状态；不读文件、不知 Pack |
| **`IEngine` / `Engine`** | Core 对外门面：`NewGame`、`Apply`、查询 API |
| **`RuleSet`** | 一局所需的全部规则参数（移动、战斗、回合、招募等）的汇总对象 |
| **Content** | `Ew4.Platform.Content`：读 Pack → 解析、校验、指纹 → `ResolvedContent` |
| **`ResolvedContent`** | 校验后的不可变内容快照；带指纹，供 Session / Builder 使用 |
| **`IScenarioBuilder`** | 由剧本 id 构建初始 `WorldState` + `RuleSet`（如 `ScenarioJsonBuilder`） |
| **Session** | `Ew4.Platform.Session`：`SessionService` 持有当前局；Catalog / Start / Apply / Leave；Save/Load |
| **Service** | `Ew4.Service`：游玩 API 编排（`POST /api/{route}?q=…`） |
| **Backend.Web** | `Ew4.Backend.Web`：Kestrel、DI、`/api/*`、发布 `Ew4.Front.Web` 静态资源 |
| **Front.Web** | `Ew4.Front.Web`：`wwwroot` 静态 UI |
| **Interface** | `Ew4.Interface`：跨层契约（如 `PlayApiRoutes`） |
| **Host / Runtime** | `Ew4.Platform.Runtime`：`HostApp` 激活 Pack、zip 导入、Workspace 预览 |
| **Editor** | `Ew4.Editor`：剧本 Toolkit、Workspace、创作 HTTP |

## 内容包（Pack）

| 术语 | 定义 |
|------|------|
| **Pack** | 含根清单 `mod.json` 的目录或 `.ew4mod`/`.zip`。原版与 Mod **同一种模型** |
| **`mod.json`** | 包唯一清单：`id`、`version`、`parent`、`requires`、`formats`，扩展段 `scenarios`（见 [mods/README.md](../mods/README.md)） |
| **Active Pack** | 当前激活的包 id，写在 `install/user/active.json` |
| **parent 补全** | Mod 声明 `parent: "base"` 时，缺路径的文件从父包整文件补齐（非字段级合并） |
| **指纹 (Fingerprint)** | 参与校验的源文件哈希汇总；存档绑定包 id + 指纹 + 引擎行为版本 |
| **ContentKind** | 注册表：某类文件的路径模式、schema、是否参与指纹 |
| **校验级别** | `Schema` → `Reference` → `Constraint` → **`SmokeStart`**（真 `NewGame` 试开局） |
| **运行时资源路径** | Pack 内 `assets/`、`source/` 等；Builder 通过 `PackAssetRead` 映射旧 `original/`、`data/` 路径 |

## 游玩与接口

| 术语 | 定义 |
|------|------|
| **`GameSession`** | Core 中的一局会话数据（含 `WorldState`、revision 等） |
| **Play 路由** | 如 `/api/catalog`、`/api/start`、`/api/action`；请求体多在查询参数 `q` 的 JSON |
| **`ClientContent`** | PlayApi 侧：从 Pack 读入后，等价于旧版 `ClientContent` 的剧本/目录/扩展数据 |
| **Workspace（创作草稿）** | `build/workspaces/<id>/`：overlay 包，可 `parent` 指向已安装 Pack；预览不改写玩家 `active.json` |

## Mod 规则扩展

| 术语 | 定义 |
|------|------|
| **声明式扩展** | 新 Mod 能力用 **JSON + Core 字段**（`rewrite/extension.json` 等），**不为新功能加 Lua** |
| **rule-api-2** | 引擎能力：API 2 拒绝 `script` 处理器；见 [rule-extensions-without-lua.md](development/rule-extensions-without-lua.md) |
| **Compat** | `RuleSet` 中的兼容开关集合，保留指定旧行为 |

## 容易混淆

- **Session（层） vs `GameSession`（类型）**：前者是服务与存档边界；后者是 Core 里的状态记录。
- **Pack vs Mod**：对用户说 Mod；代码与目录统一用 Pack / `mods/<id>/`。**不要**写「原版一条路径、Mod 另一条路径」。
- **Content vs Service**：Content 解析 Pack；`Ew4.Service` 在已激活 Pack 上响应 `/api/*`，内部仍用 `IEngine` 执行命令。
- **维护 `mods/` vs 运行 `install/mods/`**：前者编辑与 CLI 校验；后者 `EW4_HOME` 运行时读取（`build`/`install`/`web` 拷贝同步）。
- **策划 JSON vs Pack**：`maps/` 是仓库内策划；打进游戏的是 Pack 目录（维护在 `mods/`）。
