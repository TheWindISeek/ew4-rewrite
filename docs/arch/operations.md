# 操作如何完成

状态：**已定稿（与 `Ew4.Interface/Operations/Operations.cs` 同步）**  
前提：[module-responsibilities.md](./module-responsibilities.md)（谁做什么）、[interface-contracts.md](./interface-contracts.md)（契约类型）。

本文说明：**一次用户能力如何在既有模块职责下走完**，不重复模块边界定义。

---

## 1. 总链路（不可违反）

```text
Front
  ├─ POST /api/play/{route}、/api/mods* （Play / Platform；兼容 `/api/{route}`）
  ├─ /api/editor/*                     （Editor）
  └─ GET /content/*、捆绑 JSON          （Static）
            │
            ▼
      Ew4.Backend.Web
        · 仅引用 Ew4.Interface + Ew4.Service（DI）
        · 解析 HTTP → OperationDescriptor + OperationPayload
        · 或读 IGameWebPaths（wwwroot 物理目录）
            │
            ▼
      IGameOperations.Invoke
        （Ew4.Service.GameOperationExecutor）
            │
            ▼
      ├─ Static 描述符 → PlayStaticAssetService
      └─ 其余         → PlanRunner：注册表把 Request 展开成 Step
                         （play.<route>、editor.*、platform.*）
```

| 规则 | 说明 |
|------|------|
| URL 分流 | **只有 Backend** 区分 play / editor / mods / static。 |
| Service 不接 HTTP | 不注册路由、不按路径分流；只认 Descriptor + Payload。 |
| Backend 不碰 Platform | 禁止 `Ew4.Platform.*` 引用；含 `/content/*`（经 Invoke）。 |
| 存盘 | **Persistence** 平面，不经 `IEngine.Apply`。 |
| 局内回合 | **Plan `play.action`**（`route=action` 及 body 内 `op`）→ `play.apply-action` → `IPlayFacade.Engine`。 |
| 内容编辑 | **Plan `editor.*`**（Interface 类型；实现在 `Platform.Editor`，Step 直接调 `ICampaignEditorOperations` / `IEditorOperations` / `IPreviewBindingOperations`）。 |

**实现落点**

| 步骤 | 代码 |
|------|------|
| Backend 注册表 | `Ew4.Backend.Web/EditorEndpoints.cs`、`PlayEndpoints.cs`、`PlayModEndpoints.cs`、`PlayStaticFiles.cs` |
| 分类与词典 | `Ew4.Interface/Operations/Operations.cs`（`OperationCatalog`、`PlayDescriptors`） |
| 执行 | `Ew4.Service/Host/GameOperationExecutor.cs` |
| Plan 注册表与运行器 | `Ew4.Service/Plans/`（`PlanRegistry`、`PlanRunner`、`EditorPlans`、`PlayPlans`）；类型在 `Ew4.Interface/Plans/` |
| Workspace Content | `Ew4.Service/Host/EditorWorkspaceOperationsBridge.cs` |
| 静态资源 | `Ew4.Service/Host/PlayStaticAssetService.cs` |

---

## 2. 执行平面（Service 第一刀）

| 平面 | 含义 | 典型能力 |
|------|------|----------|
| **Persistence** | 槽位与序列化 | `saves`、`save`、`load` |
| **Content** | 无局内 Apply 的编排 | 选局 catalog、`start`、大本营/商店 RPC、workspace 文件、mod 面板 |
| **Plan** | 注册表展开成 Step | 局内 action、Editor（战役/将领）、预览绑定；设计见 [operation-plans.md](./operation-plans.md) |
| **Static** | 激活 mod 资源与捆绑前端 JSON | `/content/*`、`/conquest-pages.json` 等 |

已取消 `RuntimeDispatchTarget` 二次分派：Engine / Editor / Binding 不再是 Runtime 的三个目标，而是注册表里不同 Request 的 Step 组合（见 [operation-plans.md](./operation-plans.md)）。

---

## 3. 通道与 HTTP 映射（摘要）

词典与枚举的**唯一真相**：`Operations.cs` 中的 `OperationCatalog` 与 `EditorOperationKind` / `PlayRoutes` 等。

### Play · `POST /api/play/{route}`（兼容 `POST /api/{route}`）

| 平面 | route 示例 |
|------|------------|
| Content | `catalog`、`countries`、`start`、`leave`、`headquarters`、`commerce`、… |
| Plan | `action`（body 内 `op`：move、attack、end、…），Request 名 `play.action` |
| Persistence | `saves`、`save`、`load` |

Backend 用 `PlayDescriptors.Describe(route)` 组装 descriptor。

### Editor · `/api/editor/*`

| 平面 | 能力 |
|------|------|
| Content | workspaces CRUD（创建可选 `mode`）、capabilities、maps（list/describe/preview）、validate、export（可选 `standalone`）、campaigns 列表、files 读写 |
| Plan | preview enter/refresh/exit、preview 状态（资源键 `binding`） |
| Plan | workspace 下 campaign create/get/put/edit/roundtrip；commanders（先 `preview.ensure-binding`）；btl-to-json |

逐端点的请求/响应、错误码与场景 JSON 格式见 [editor-api.md](./editor-api.md)。

Backend 用 `EditorOperationDescriptor(EditorOperationKind)` + `EditorWorkspacePayload`。

### Platform · `/api/mods*`、`/api/mod-prepare`、`/api/mod-import`

`PlatformModOperationDescriptor` + 对应 `OperationPayload`（JSON / 文件 / 表单）。

### Static · `GET /content/*` 等

`StaticHttpAssetDescriptor(RequestPath)` + `StaticAssetPayload` → `PlayStaticAssetService`（读激活 mod 快照或 wwwroot 捆绑 JSON）。

---

## 4. 能力树（产品视角）

```text
EW4 客户端能力
├─ Play
│  ├─ 选局与元数据 (Content)
│  ├─ 大本营与商店 (Content)
│  ├─ 地图回合 (Plan: play.action)
│  └─ 槽位 (Persistence)
├─ Editor
│  ├─ 工作区 (Content)
│  ├─ 战役 / 将领 (Plan: editor.*)
│  └─ 预览 (Plan: editor.preview.*)
├─ Platform (Content 编排 mod)
└─ Static
```

---

## 5. Engine 与 Editor（同层不同域）

| | Engine | Editor |
|--|--------|--------|
| 对象 | 局内 `WorldState` + `RuleSet` | 战役 / 将领等内容定义 |
| 结果 | 新局状态 | Interface DTO；写盘由 Editor/Persistence 完成 |
| 入口 | `IPlayFacade.Engine`（Step `play.apply-action`） | `ICampaignEditorOperations` / `IEditorOperations`（Editor 的 Step） |

**Content** 提供载入与校验，不是 HTTP 总线。

**游玩域**：Web 热路径为 `PlayHost` + `PlayCoordinator`（revision 与 `HostApp` 激活同步），游玩编排落在 `Ew4.Service.Play`；一局内的行动队列/撤销在 `Ew4.Platform.Play`（`Match/`）。请求层面 `action` 是一个 Plan，与 Editor 的 Plan 同级。

**Persistence 平面（槽位）**：`save` / `load` / `saves` 的业务编排在 `PlaySession`；**字节落盘**经 `Ew4.Platform.Persistence.Serialization`（`PlaySlotPersistence`、`SlotSaveEnvelope`、`SessionSave`；`EngineSlotPersistence` 供 Tool/CLI `CliEngineGame`）。

---

## 6. 绑定与创作上下文

- **游玩**：`HostApp` 激活已安装 mod → `ContentBinding`（非 workspace）。
- **预览 / 将领网关**：`ContentBindingKind.WorkspacePreview`；将领相关 Plan 的第一个 Step `preview.ensure-binding` 会 `Preview.Enter(workspaceId)`（物理路径由 `WorkspaceStore` 在 Service 内解析）。
- **战役 workspace 路径**：多数 `ICampaignEditorOperations` 方法显式带 `workspaceId`，不依赖绑定。

---

## 7. Code review 快问

| 问题 | 应落点 |
|------|--------|
| 谁解析 URL？ | **Backend** |
| Backend 能否引用 Platform？ | **否** |
| 谁 `Classify`？ | **Service**（`GameOperationExecutor`） |
| 谁分 Engine / Editor？ | **Classifier → Plan**；`PlanRegistry` 里的 Request 决定 Step 序列 |
| JSON 谁写 HTTP body？ | **Backend** 写 `OperationResponse`；Service 填字符串/字节 |
| Service 为何能见到 `Ew4.Platform.Editor`？ | **仅 DI 组合根与 Bridge/Adapter**；业务分派仍经 Interface |

---

## 8. 扩展新终端能力

1. 在 `Operations.cs` 增加 catalog 叶子与 `EditorOperationKind` / route 常量（如需）。
2. 在 `RequestNames` 里让 descriptor 映射到 Request 名。
3. 在对应 `IPlanModule`（`EditorPlans` / `PlayPlans` / `ModPlans`，或新模块）里登记 Request → Step。
4. Backend 增加路径 → descriptor + payload 映射（一条注册，避免双轨）。
5. `Ew4.Test.Interface`：分类与关键 Invoke 行为。
