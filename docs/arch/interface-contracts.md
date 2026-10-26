# 接口定义（Ew4.Interface）

状态：**与 `src/Ew4.Interface` 同步**  
模块职责见 [module-responsibilities.md](./module-responsibilities.md)；操作怎么走见 [operations.md](./operations.md)。

`Ew4.Interface` **不引用**其它 `Ew4.*` 项目，是 Backend、Service、测试与文档的**共同词汇表**。

---

## 1. 调用总契约

| 类型 | 说明 |
|------|------|
| **`IGameOperations`** | Backend 对 Service 的**唯一**业务入口：`Invoke(descriptor, payload)`、`ActiveContentRevision`。 |
| **`OperationDescriptor`** | 操作身份（通道 + 种类），与 HTTP 解耦。 |
| **`OperationPayload`** | 与 descriptor 配套的载荷（body、query、路由参数已解析进字段）。 |
| **`OperationResponse`** | 文本 JSON、二进制、可选 HTTP 状态码（201、204、404）。 |
| **`IGameWebPaths`** | 解析后的 **wwwroot 目录**（物理静态中间件用；动态 `/content` 仍走 Invoke）。 |
| **`Ew4HostLaunchOptions`** | 启动参数解析结果（`RepositoryRoot`、`InstallHome`）→ `AddEw4GameHost`。 |

```text
Backend → IGameOperations.Invoke(OperationDescriptor, OperationPayload) → OperationResponse
```

---

## 2. Operations（词典与分类）

**源文件**：`Ew4.Interface/Operations/Operations.cs`（文件头注释为阅读入口）。

| 概念 | 用途 |
|------|------|
| **`OperationChannel`** | Play / Editor / Platform / Static |
| **`PlayDescriptors.Describe`** | play route → `PlayOperationDescriptor`（Backend 用） |
| **`OperationCatalog`** | 能力树终端 id（与产品/文档对齐） |

### Descriptor 类型（摘要）

| Descriptor | 通道 | Backend 典型来源 |
|------------|------|------------------|
| `PlayOperationDescriptor` / `PlayContentRouteOperation` / `PlayPersistenceRouteOperation` / `PlayEngineRouteOperation` | Play | `PlayDescriptors.Describe(route)` |
| `EditorOperationDescriptor` | Editor | `EditorOperationKind` + HTTP 注册表 |
| `PlatformModOperationDescriptor` | Platform | `/api/mods` 等 |
| `StaticHttpAssetDescriptor` | Static | 请求路径 `/content/…` 或捆绑 JSON |

### Payload 类型（`Host/OperationPayload.cs`）

| Payload | 用途 |
|---------|------|
| `PlayBodyPayload` | Play POST body 或 `q=` |
| `PlatformJsonPayload` / `PlatformFilePayload` / `PlatformFormCommitPayload` | Mod 导入流水线 |
| `EditorWorkspacePayload` | workspaceId、路径、campaignId、body等 |
| `StaticAssetPayload` | 空；路径在 `StaticHttpAssetDescriptor` |

### Editor 终端枚举

`EditorOperationKind`：workspace 系列（含 `WorkspaceCapabilities`）、`Map*`（List/Describe/Preview）、preview 系列、`Campaign*`（含 `CampaignCreate`）、`Commander*`、`Legacy*`、`BtlToJson`。  
与 HTTP 的对应关系见 [operations.md §3](./operations.md#3-通道与-http-映射摘要)。

### Play 路由常量

- `Ew4.Interface.Play.PlayRoutes` — RPC route 名（`catalog`、`action`、…）。
- `Ew4.Interface.Front.PlayApiRoutes` — 与 HTTP 路径相关的 mod 路由名。

---

## 3. Play 门面

| 类型 | 说明 |
|------|------|
| **`IPlayFacade`** | Service 内游玩总门面：`Persistence`、`Content`、`Engine`、`Json`。 |
| **`IPlayJsonInvoker`** | `InvokeEnvelope(revision, route, body)` — Web 与宿主 JSON 桥。 |
| **`PlayRoutes`** | route 常量与 `All` 列表。 |
| **`PlayResult` / `PlayError`** | 业务结果与错误码（Service 内部与 JSON 信封）。 |

实现：`Ew4.Service.Play.PlayHost`（同时实现 `IPlayFacade`）。  
细分 handler 注册见 `PlayRouteRegistry`（实现细节，非 Interface）。

---

## 4. Mod

| 类型 | 说明 |
|------|------|
| **`IModOperations`** | 面板 JSON、激活/删除、导入 stage/commit。 |
| **`IModSnapshotLoader`** / **`IModSnapshot`** | 载入与激活视图（见 §7）。 |

---

## 5. Editor

| 类型 | 平面 | 说明 |
|------|------|------|
| **`IEditorWorkspaceOperations`** | Content | 列表/创建（可选模式）/能力集/地图目录/校验/导出（可选独立）/文件；返回 JSON 字符串或字节。 |
| **`IEditorOperations`** | Plan（Editor 域 Step 调用） | 将领表：`ListCommanders`、`GetCommander`、`SkillCatalog`、`ApplyCommanderEdit`。 |
| **`ICampaignEditorOperations`** | Plan（Editor 域 Step 调用） | 战役 create/GET/PUT/edit/roundtrip、btl-to-json 等。端点细节见 [editor-api.md](./editor-api.md)。 |
| **`EditorOutcome<T>`** | | 成功/失败与 `EditorError`。 |
| **`CommanderModels`** | | `CommanderEntry`、`CommanderEdit*` 等 DTO。 |
| **`EditorRoutes`** | | 路由片段常量（文档/客户端参考）。 |

实现：`Ew4.Platform.Editor.InterfaceAdapters`；Service 的 `EditorPlans` 或 `GameOperationExecutor` 经这些接口调用。

---

## 6. 绑定与 Plan

| 类型 | 说明 |
|------|------|
| **`PlanKey<T>` / `IStep` / `StepSpec` / `PlanContext`** | `Ew4.Interface.Plans`：Step 的读写声明与一次请求的键值存储，见 [operation-plans.md](./operation-plans.md)。 |
| **`IContentBindingSource`** | 当前 `ContentBinding`（Engine 与 Editor 共享上下文）。 |
| **`ContentBinding`** / **`ContentBindingKind`** | revision、modId、workspace 预览 id。 |
| **`IPreviewBindingOperations`** | Enter / Refresh / Exit / `GetStateJson`。 |

Service 实现：`PreviewBindingOperations`（包装 `HostApp`）；Plan 注册表与运行器在 `Ew4.Service.Plans`。

---

## 7. Mod 快照载入

| 类型 | 说明 |
|------|------|
| **`IModPackHandle`** | 已打开的包；Persistence **`IModSource`** 实现。 |
| **`IModSnapshot`** / **`IModSnapshotLoader`** | 激活视图与载入（merge + 校验）；实现类 **`ModSnapshotLoader`** 在 Content。 |
| **`ModContentRef`** | 合并视图中的逻辑路径索引。 |

---

## 8. Common / Front

| 类型 | 说明 |
|------|------|
| **`ValidationLevel`**、`Diagnostic`、`Severity` | Editor 校验与导出诊断。 |
| **`OperationException`** | Invoke 可映射为 4xx 的预期失败（`Code` + `Message`）；`ContentError`、`ModError` 等为其子类。 |
| **`ApiFailure`** | Play API 标准错误 JSON（`ok`、`code`、`message`）。 |

---

## 9. 禁止再出现的形状

- Backend 注入或引用 `Ew4.Platform.*`。
- Backend 直注 `IWorkspaceService`、`CampaignDocumentService` 等 Platform 服务。
- Service 按 HTTP 路径分流（`/api/play` vs `/api/editor`）。
- 聚合 Play+Editor 的「Session 总线」接口（`IGameHost`、`IPlayHost` 等已删除）。
- 在 Interface 层放 IO、规则演算或磁盘路径常量（install 布局在 Service 启动配置，不经 Interface 暴露 Platform 类型）。

---

## 10. 与实现项目的对照

| Interface | 主要实现位置 |
|-----------|----------------|
| `IGameOperations` | `Ew4.Service.Host.GameOperationExecutor` |
| `IEditorWorkspaceOperations` | `Ew4.Service.Host.EditorWorkspaceOperationsBridge` |
| `IStep`（Editor / Play 域） | `Ew4.Service.Plans.EditorPlans`、`PlayPlans` |
| `IEditorOperations` / `ICampaignEditorOperations` | `Ew4.Platform.Editor.InterfaceAdapters` |
| `IPlayFacade` | `Ew4.Service.Play.PlayHost` |
| `IModOperations` | `Ew4.Service.Host.ModOps` → `Ew4.Platform.Mod`（Catalog / Import） |

Play handler、`IEngine` 等实现细节在 `Ew4.Service.Play` 与 `Ew4.Platform.Engine`，不在 Interface 重复。
