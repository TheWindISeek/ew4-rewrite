# 程序模块职责与目标结构

状态：**已定稿（拆分与重构依据）**  
关联：[interface-contracts.md](./interface-contracts.md)、[operations.md](./operations.md)。

---

## 1. 设计原则

1. **Persistence 是唯一磁盘边界**：读/写 install 与 user 目录、导入落盘、从文件解析到内存存储形态（字节、JSON、解压树）。**Mod 与 Content 不直接 `File.*`、不持有 install 路径。**
2. **Mod**：在 Persistence 已读出的数据之上，做激活、`parent` 合并、逻辑路径、指纹；编排导入时**命令 Persistence 写盘**。
3. **Content**：只对接 **Mod 视图**（+ 经 Persistence 读写的**槽位存档**等）；静态表、战役 codec、**唯一开局管线**。
4. **Engine**：内存规则演算；`IEngine.Apply`；无 IO。**不内置原版数值表**（技能参数、AI 配方等由 Content 提供）；规则配置只在 `RuleSet.Validate()` 校验一次，规则函数只检查运行时状态。
5. **Service**：游玩与局外编排；执行经 **Interface** 分派到 Content / Plan；**不**按 HTTP 路径分流。组合根可注册 Editor Adapter，对外仅 Interface（见 [operations.md](./operations.md)）。
6. **Backend**：HTTP → **`IGameOperations.Invoke`**；仅 **Interface + Service**；wwwroot 来自 **`IGameWebPaths`**。
7. **Play**（`Ew4.Platform.Play`，原 Runtime）：游玩域，一局内的行动队列、撤销、回合流转（`Match/`）。与 Editor 同级，不再承担"绑定与分派"。`HostApp`（Pack 激活、workspace 预览）移到 `Ew4.Service/Host`；请求 → Step 的对应由 `Ew4.Service/Plans` 的注册表负责。
8. **单实现**：不为多套后端抽 `I*` 接口层；Tool 与 Service 共用同一套 Persistence/Mod API。
9. **先收束再扩展**：已砍掉独立地图工程文件（原 `MapDocument`）；未实现能力不写进目标树。

---

## 2. 程序集职责

| 程序集 | 职责 | 不该碰 |
|--------|------|--------|
| **Ew4.Interface** | 路由与 Play 契约 | IO、规则 |
| **Ew4.Platform.Engine** | `WorldState` + `RuleSet` 演算 | 磁盘、Mod 合并 |
| **Ew4.Platform.Ai** | 读 `WorldState` 规划，产出与玩家相同的 `EngineCommand`（`AiPlanner.Next`）；自身无状态 | 直接修改世界、磁盘 |
| **Ew4.Platform.Persistence** | 全部磁盘 IO 与文件级解析 | 游戏规则、parent 合并语义 |
| **Ew4.Platform.Mod** | 激活、列表、导入编排、合并视图 | 直接读盘 |
| **Ew4.Platform.Content** | Mod 视图 → catalog、战役、开局 | install 路径、扫 mods 目录 |
| **Ew4.Platform.Play** | 游玩域：`FlowQueue`、`MatchFlow`、`UndoSlot` | HTTP、磁盘、Pack 激活 |
| **Ew4.Platform.Editor** | 内容域操作实现；写 overlay 经 Persistence | 被 Backend 引用；HTTP 用例不得绕过 Invoke |
| **Ew4.Service** | `GameOperationExecutor`、`PlayHost`、`HostApp`、`Plans/`（注册表与 Step）、Bridge | HTTP 分流、直读 mods 目录 |
| **Ew4.Backend.Web** | HTTP → **`IGameOperations.Invoke`**；wwwroot 只读 **`IGameWebPaths`** | **任何** `Ew4.Platform.*` 引用、直调 Platform 类型 |
| **Ew4.Front.Web** | 浏览器 UI | Platform 引用 |
| **Ew4.Tool** | 离线 PackBuild；读盘只经 Persistence | 热路径 |

### 依赖方向（目标）

```text
Front  ←  Backend  →  Service  →  Content
              │              ├→  Play  →  Engine
              │              ├→  Editor（Step 经 Interface 调用）
              │              └→  Mod → Persistence
              └  /api/play/* 与 /api/editor/* 仅在此分流

Tool → Persistence, Mod, Content, …
```

### 数据流

```text
磁盘 → Persistence（IO + 文件解析）
        → Mod（激活 + 合并 + 逻辑资源）
        → Content（StaticData / CatalogLoad / Campaign / Start）
        → Engine（Apply）
        → Service（持局 + 投影）
        → Backend → Front

存盘：Service → Persistence.Serialization（不经 Apply）
```

---

## 3. 目标目录树（`src/`）

```text
src/
├── Ew4.Interface/
│   ├── Play/
│   └── Front/
│
├── Ew4.Platform.Persistence/       # 唯一磁盘边界
│   ├── Layout/                     EW4_HOME、mods 根、saves 路径
│   ├── User/                       active.json 等
│   ├── Packages/                   已安装 mod 目录/zip、读包内文件、Install
│   ├── Serialization/              WorldSave, SessionSave, SlotSaveEnvelope, 槽位
│   └── Import/                     导入 staging、原子写（供 Mod/Tool）
│
├── Ew4.Platform.Mod/
│   ├── ModInstallGuard.cs          安装/激活/删除 parent 与引擎约束
│   ├── Activation/
│   ├── Catalog/                    面板列表（ModCatalogService）
│   ├── Import/                     Stage/Commit 编排
│   ├── Merge/                      parent 合并（自 Content 迁入）
│   └── Snapshot/                   载入索引、revision
│
├── Ew4.Platform.Content/
│   ├── StaticData/                 非规则数据（原 Original/）
│   ├── CatalogLoad/                表与扩展载入（原 Build/）
│   ├── Campaign/                   场景 JSON 编辑（EditSession、patch、capabilities）
│   ├── Account/                    交易/编队/继承/消耗品/酒馆规则（SessionAccountActions，不经 IEngine.Apply）
│   ├── Start/                      唯一开局（ScenarioJsonBuilder、TerritorialJurisdictionBuilder、FactionAppearanceResolve 等）
│   └── RepoPaths/                  仓库内路径（Tool/CI，非 EW4_HOME）
│
├── Ew4.Platform.Engine/
│   ├── Core/, Commands/, Systems/, World/, Combat/
│   ├── Scenario/, Session/（战场会话聚合）, Account/（仅账户状态数据）, Extensions/, Economy/, Meta/
│
├── Ew4.Platform.Ai/                # 抽自 Engine：Planning/（纯打分）与 World/（WorldState→Facts→EngineCommand）
│
├── Ew4.Platform.Play/              游玩域：Match/（FlowQueue、MatchFlow、UndoSlot）
│
├── Ew4.Platform.Editor/
│   ├── Workspace/, Commander/, Campaign/, Conquest/
│
├── Ew4.Service/
│   ├── Host/
│   ├── CliEngineGame.cs            Tool/CLI 薄局内编排（Web 主路径在 Play/）
│   └── Play/
│
├── Ew4.Backend.Web/
├── Ew4.Front.Web/
└── Ew4.Tool/
    └── PackBuild/                  原 Import/
```

**迁移期对照**

| 目标 | 状态 |
|------|------|
| `Persistence/Packages`（`DirectoryModSource`、`FileModRepository` 等） | **已就位**（文件名已与类型对齐） |
| `Content/CatalogLoad`、`StaticData`、`Campaign` | **已就位**（`Original/`、`ScenarioEdit/` 已移除） |
| `Persistence/User` | 原 `Runtime/ActivePackStore` → `ActiveModStore` |
| `Persistence/Serialization` | 原 `Ew4.Platform.Session` |
| Tool 向局内编排 | **`CliEngineGame`**（单文件；原 `Service/Session` 已合并并改名） |
| `Mod/Merge`、`Mod/Snapshot` | **已就位**（自 Content 根目录 / `Snapshot/` 迁入） |
| `Mod/Catalog`、`Mod/Import`、`Mod/Activation`（`IModHost`） | **已就位**（自 `Ew4.Service/Mod` 迁入；`HostApp` 实现 `IModHost`） |
| `IModSnapshotLoader` / `IModSnapshot` | **已就位**（契约在 **Interface.Mod**；`ModSnapshotLoader` 在 Content；`ModSnapshotIndex` 在 Mod） |
| `Ew4.Platform.Session` | **已删除**（序列化在 Persistence；局内在 Service） |
| `InstallLayout` / `RepositoryPaths` | **已就位**（Persistence.Layout；自 Runtime / Content） |

---

## 4. Content 内命名（取代 Legacy / Bootstrap）

| 目标文件夹 | 原目录 | 含义 |
|------------|--------|------|
| **StaticData** | `Original/` | 非规则数据：XML/BTL 域模型 |
| **CatalogLoad** | `Build/` | 从 Mod 视图载入 catalog、扩展 |
| **Campaign** | `ScenarioEdit/`、`Importers/` | 战役编解码 |
| **Start** | 根目录 builder/host | 唯一开局管线 |

---

## 5. 词汇表

| 术语 | 含义 |
|------|------|
| Persistence | 磁盘与文件解析 |
| Mod | 激活与合并后的内容视图 |
| StaticData | 封存表/BTL 结构（非 Engine 回合逻辑） |
| CatalogLoad | catalog 与扩展载入 |
| Campaign | 战役 codec |
| PackBuild | Tool 离线造包 |

---

## 6. Code review 快问快答

| 问题 | 落点 |
|------|------|
| 写槽位存档 | Persistence.Serialization |
| 读 `mods/foo/mod.json` | Persistence.Packages |
| 激活哪个 mod | Mod → Persistence.User |
| 合并 parent | Mod.Merge |
| 解析 def_commander | Content.StaticData / CatalogLoad |
| 移动消耗 | Engine |
| `/api/play/*` vs `/api/editor/*` 谁映射 | **Backend.Web** → Service |
| Service 编排经谁 | **Interface** → Content / Plan（Step）/ Adapter |
| Engine / Editor 谁分派 | **`PlanRegistry`**：Request → Step 序列；局内 action 的 Step 进入 `Ew4.Service.Play`（`PlayHost` + `HostApp` 绑定） |
| POST 局内 action（游玩） | Backend → `IGameOperations` → `IPlayFacade.Engine` → `PlaySession` → `IEngine.Apply` |
| Play 槽位 / account 文件 IO | **`SaveDirectoryLayout` + `SaveSlotId`**、`PlaySlotPersistence`、`PlayerAccountPersistence`；Service 只序列化 `PlaySlot` 业务 JSON |
| import-base | Tool → Persistence + Content |

---

## 7. 同目录另两篇

- **[interface-contracts.md](./interface-contracts.md)** — Interface 类型定义  
- **[operations.md](./operations.md)** — 操作如何经 Invoke 完成  

作者包规范：[mods/README.md](../../mods/README.md)。
