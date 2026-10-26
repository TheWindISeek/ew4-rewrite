# 操作编排：Request / Step / Plan（设计草案）

> 状态：**框架已实现；`play.action` 与 `editor.*`（战役/将领/预览）已迁入注册表；全部 play route（`play.<route>`，含 start/load/save/saves 等）也已迁入，每个 route 先是 3 个 Step 的薄封装；workspace（EditorPlans）与 mod 面板（ModPlans）也已迁入；`Content`/`Persistence` 平面已删除，执行器只剩 Plan 与 Static**（见 §11）。目标是把"用户发来的操作"和"底层模块的原子操作"分开，由 Service 里一张显式注册表把二者对应起来，并负责仲裁。
> 相关：[operations.md](./operations.md)（现状词典）、[module-responsibilities.md](./module-responsibilities.md)、[editor-api.md](./editor-api.md)。

## 1. 动机

现状的问题：

1. `EditorOperationKind` 一个枚举同时扮演"HTTP 请求名"和"底层方法名"，二者恰好一一对应，概念上却是两回事。
2. 没有"组装"的位置。`start` 在 `PlaySession.Start` 里是一整段顺序代码（校验、选局、建世界、写账号、自动存档……），加音乐只能改这段代码。
3. `Runtime` 名不副实：`RuntimeOperationDispatch` 把 Engine / Editor / Binding 三类不相干目标塞进同一个 switch；而游玩逻辑和 Editor 逻辑本应是**同级、互不相关**的两个域。
4. 多个请求同时到达时没有统一仲裁点。

核心准则（沿用）：**简化**。新增能力（如音乐）应当是"加一个模块 + 往表里加一项"，不改已有代码。

## 2. 分层与词汇

```text
用户层     Request     用户意图                 例：start-campaign、end-turn、edit-scenario
             │
Service    Registry    Request → Plan（有序 Step）；仲裁；合并响应
             │
底层层     Step        模块暴露的原子操作        例：content.load-scenario、audio.pick-bgm
             │
模块       Engine / Content / Editor / Persistence / Audio / Play(游玩域) …
```

- 两套词汇**互不认识**，只有 Service 的注册表知道对应关系。
- Step 由**模块自己声明并拥有**；模块不关心谁调用它。
- Editor 域与游玩域（Runtime）同级。游玩域内部的差异（战役 / 征服 / 教程）体现为**不同的 Plan**，而不是 Runtime 里的 `if`。

### 2.1 Step 的粒度

一个 Step = 某模块里**有明确输入输出、可单独测试**的一个动作。不细到一行逻辑，也不粗到整个流程。Plan 读起来应当像剧本，而不是汇编。

## 3. 核心类型（草案）

```csharp
// ── Step：模块暴露 ──
public interface IStep
{
    string Id { get; }                       // "content.load-scenario"
    StepSpec Spec { get; }                   // 读/写哪些键、是否必需、超时
    void Run(PlanContext ctx);               // 对外同步；内部可自行异步并等待
}

public sealed record StepSpec(
    IReadOnlyList<PlanKey> Reads,
    IReadOnlyList<PlanKey> Writes,
    TimeSpan? Timeout = null);          // 必需/可选由 Plan 里的条目决定，不在 Step 上

// ── 键：类型化，声明时决定是否对外暴露 ──
public sealed record PlanKey<T>(string Name, Expose? Expose = null);
public sealed record Expose(string Slot, SlotKind Kind);   // Single | Append
```

注册表声明：

```csharp
registry.Request("start-campaign")
    .Resource("session:{slot}")                           // 资源键（见 §6）
    .Step<ValidateLocalSelection>()
    .Step<CheckCampaignUnlocked>()
    .Step<LoadScenario>()                                 // 写 World / Rules
    .Step<ResetFlow>()
    .Step<AdoptSession>()
    .Step<CommitProfile>()
    .Step<AutoSave>()
    .Step<PickBgm>(optional: true)                        // 写 Effects
    .Step<RenderView>();                                  // 写 View
```

`.Step().Step()` 只是书写语法糖，底层是每个 Step 的 `Reads/Writes` 声明。

## 4. PlanContext 与响应（声明式）

### 4.1 单个 PlanContext

- 一次请求一个 `PlanContext`，本质是 `Dictionary<键, 值>`，**只在服务端、本次请求内**存活。
- Step 之间通过键传数据；Step 只依赖它声明要读的键，不依赖具体是哪个上一步。
- 不需要第二个"响应 map"。**是否给前端**由键在声明处用 `Expose` 标记：

```csharp
public static class Keys
{
    public static readonly Key<GameSession>   Session = new("session");                 // 内部，不暴露
    public static readonly Key<RuleSet>       Rules   = new("rules");                   // 内部，不暴露
    public static readonly Key<PlayView>      View    = new("view",    new("view",    SlotKind.Single));
    public static readonly Key<EffectCue>     Cue     = new("cue",     new("effects", SlotKind.Append));
    public static readonly Key<string>        Notice  = new("notice",  new("notices", SlotKind.Append));
}
```

### 4.2 响应合成

- **Plan 全部成功后**，Service 才把带 `Expose` 的键按槽位复制进响应；任何必需 Step 失败，已写入的内容一律丢弃，只返回错误。
- 槽位类型：`Single`（只允许一个写入者）、`Append`（按 Plan 顺序追加成数组）。
- 响应顶层字段就是槽位名：

```json
{ "view": { … }, "effects": [ { "play": "campaign" } ], "notices": [] }
```

初始槽位：`view`（局面视图）、`effects`（音效/特效指令，由客户端执行）、`notices`（提示）、`diagnostics`（可选 Step 的失败信息，开发期可见）。

### 4.3 "只给前端看"的含义

`Expose` 只表示"结果里带上这个值"。**事情在 Step 里已经做完了**：世界已建、存档已写；`effects` 里的 `play` 只是把"该放什么"交给前端去执行。

### 4.4 启动时校验

注册表构建时检查并在不通过时直接启动失败：

1. 每个 Step 读的键，在它之前有 Step 写过（或是请求输入键）。
2. `Single` 槽位只有一个写入者。
3. Step Id 唯一；Plan 里引用的 Step 都已注册。
4. 资源键模板里的占位符都能从请求输入中取到。

## 5. 失败语义

| 情形 | 行为 |
|---|---|
| 必需 Step 抛 `ContentError` / `RuleViolation` 等 | 中止 Plan，丢弃全部暴露值，经现有 `OperationInvokeFaults` 转 **400** `{code,message}` |
| 可选 Step 抛异常 | 框架捕获，写入 `diagnostics`，Plan 继续 |
| Step 超时 | 必需：按失败处理；可选：按可选失败处理 |
| 未预期异常 | 与现状一致，向上抛（500） |

- **异常只表示失败**，不用来传成功信息。
- 可选的 `Undo`：只在确有需要的 Step 上提供，失败时按逆序执行已完成 Step 的 `Undo`；不强求每个 Step 都有。
- 开局类 Plan 的"原子性"目前由"先全部算好再一次性替换会话状态"保证（见 §8 样例），不依赖 `Undo`。

## 6. 仲裁（Service 的第二个职责）

- 每个 Plan 声明**资源键**：`session:{slot}`、`workspace:{id}`、`save:{slot}`、`mod`。
- 同键的 Plan 串行；不同键并行。
- 冲突策略按 Plan 配置：`Queue`（默认，排队）、`Reject`（409）、`Replace`（只认最新，用于连点开局之类）。
- **边界**：Service 仲裁"请求之间"；`FlowQueue` 管"同一局内的行动序列"。两层不重叠。

### 6.1 同步对外，内部可异步

- 对外一律同步：`Invoke` 返回时 Plan 已经结束。
- Step 内部可自行异步（写盘、预加载），但：
  1. Step 返回时，后台工作要么已完成，要么只剩不再读写共享状态的收尾；
  2. 可选 Step 的后台异常记 `diagnostics` 并吞掉；
  3. 每个 Step 可设超时，避免卡住资源键。
- 音乐在客户端播放，服务端的"放音乐"Step 实际只是产出一条 `effects`，通常是纯同步的小 Step。

## 7. 局内 action 也走注册表

此前"高频走旁路"的想法**撤回**。理由：

| 判据 | 开局 / 存读档 / 编辑器 | 局内 move/attack |
|---|---|---|
| 需要跨模块组合 | 是 | 目前否 |
| 需要请求间仲裁 | 是 | 是（同局串行） |
| 自带事务语义 | 弱 | 强（撤销槽、FlowQueue、AI 回合） |

- 频率不是判据：预先构造好的 Plan，每次调用只是几次委托，开销可忽略。
- 单独为 action 开旁路会形成例外，违背"简化"；且**攻击/占城音效**正是"在 action 之后追加可选 Step"的典型场景（Step 消费 `EngineEvents`），旁路会让它重新变成改 Runtime。
- 做法：`action` 的 Plan 为静态对象，首个 Step 是 `play.apply-action`（内部仍走 `FlowQueue`/`UndoSlot`），其后可挂 `audio.cue-from-events` 等可选 Step；资源键 `session:{slot}`。

## 8. 样例：`start-campaign`

对照现状 `PlaySession.Start` / `PlayContent.Start`，拆成：

| # | Step Id | 所属模块 | 读 | 写 | 必需 | 对应现有代码 |
|---|---|---|---|---|---|---|
| 1 | `play.validate-local-selection` | 游玩域 | `Request` | — | 是 | `ValidateLocalSelection` |
| 2 | `play.end-battle-profile` | 游玩域 | — | — | 是 | `EndBattleProfile` |
| 3 | `play.check-unlocked` | 游玩域 | `Request`, `Account` | — | 是 | `CampaignProgress.Accessible` |
| 4 | `content.open-battle` | Content | `Request` | `Battle` | 是 | `PlayContent.Battle` |
| 5 | `content.build-start` | Content | `Battle`, `Request`, `Account` | `Session`, `Rules` | 是 | `StaticDataScenarioHost.Start` |
| 6 | `content.apply-jurisdiction` | Content | `Battle`, `Session` | `Session` | 是 | 辖区应用 |
| 7 | `content.apply-victory` | Content | `Battle`, `Session`, `Rules` | `Session`, `Rules` | 是 | `ExtensionVictory` + `ScenarioRules.Evaluate` |
| 8 | `play.adopt-session` | 游玩域 | `Session`, `Rules` | `Session` | 是 | `Adopt` + `SettleBattle` + `BattleId` |
| 9 | `play.reset-flow` | 游玩域 | — | — | 是 | `flow.Clear()` / `undo.Clear()` |
| 10 | `persist.commit-profile` | Persistence | `Session` | — | 是 | `SetAccount` + `CommitProfile` |
| 11 | `play.install-session` | 游玩域 | `Session`, `Request` | — | 是 | 写 `game` / `country` / AI 种子 |
| 12 | `persist.autosave` | Persistence | `Session` | — | 是 | `AutoSave` |
| 13 | `audio.pick-bgm` | **Audio（新）** | `Session` | `Cue` | **否** | （新增） |
| 14 | `play.render-view` | 游玩域 | `Session` | `View` | 是 | `View()` |

要点：

- 1–7 只**计算**，不改会话状态；8–12 才落地；因此失败发生在落地之前时，会话保持原样。第 11 步是"替换会话状态"的唯一提交点。
- 加音乐 = 新增 Audio 模块 + 第 13 行；其余行不动。
- 读档（`load`）与开局共用 4–7 的大部分 Step（`content.open-battle`、`content.build-start` 等），差别只在 Plan 的拼装；这也是拆 Step 的主要收益。
- 响应：`view`（第 14 步）、`effects`（第 13 步）；第 13 步失败时 `effects` 为空，`diagnostics` 里有原因，开局本身成功。

## 9. 与现有代码的对应与迁移方向

| 现有 | 去向 |
|---|---|
| `OperationCatalog` / `OperationDescriptor` | 成为 Request 表的基础（Request 名 + 输入解析） |
| `OperationClassifier` + `GameOperationExecutor` 的 switch | 被注册表取代 |
| `RuntimeOperationDispatch`（Engine / Editor / Binding） | 拆开：Editor、Binding 各自成域；Runtime 仅保留游玩域 |
| `PlayRouteRegistry`（route → 方法） | 并入注册表；每条路由变成一个 Plan |
| `PlaySession` 大方法（Start / Load / …） | 按 §8 拆成 Step，状态由 Step 经 `PlanContext` 传递 |
| `ICampaignEditorOperations` 等 Interface | 其方法即 Editor 域的 Step 实现 |
| `OperationInvokeFaults` | 保留，用于失败转 400 |

建议迁移顺序（每步保持可编译、测试全绿）：

1. 引入 `PlanContext` / `Key` / `StepSpec` / 注册表与启动校验，附单元测试（不接业务）。
2. 先迁**一个低风险域**验证模型，例如 Editor 的 campaign get/put/edit（本来就是原子能力）。
3. 迁 `start` / `load`（§8），拆 `PlaySession`。
4. 迁 `action`（§7），加 Audio 模块作为"加模块不改旧码"的验证。
5. 迁其余 Content 路由、Persistence、Platform mod 面板；（已完成）删除 `OperationClassifier` 与旧 switch。

## 10. 待定项

- Step 的 `Undo` 具体形态与哪些 Step 需要。
- `Replace` 策略下，被取代的请求返回什么（建议 409 + 专用 code）。
- `PlanContext` 是否需要"只读快照"给可选 Step，防止可选 Step 改动必需 Step 的数据（建议：可选 Step 只能写自己声明的键，由框架检查）。
- Plan 是否允许条件分支（建议：先不允许，用"不同 Request 对应不同 Plan"表达；真有需要再加 `When`）。
- mod 对 Plan 的扩展（暂不做；留出"向某 Request 追加可选 Step"的扩展点）。

## 11. 实现状态（本次落地）

**已实现**

- `Ew4.Interface/Plans/`：`PlanKey<T>`、`Expose`/`SlotKind`、`IStep`/`StepSpec`、`PlanContext`、`PlanKeys`。
  - `PlanContext` 的写入先放 Step 级暂存区，Step 成功才提交；Step 只能读写自己声明的键，越权抛 `InvalidOperationException`。
- `Ew4.Service/Plans/`：`PlanRegistry`（`Request(...).Step(...).Resource(...).Done()` + `Validate()`）、`PlanRunner`、`RequestNames`、`Steps.Of(...)`、`ConflictPolicy`（`Queue` / `Reject`）。
  - 启动校验：读前必有写、`Single` 槽位唯一写入者、Step id 与 Request 名唯一；可选 Step 的写入不算作后续 Step 的输入。
  - 失败：必需 Step 的异常原样上抛，由 `OperationInvokeFaults` 映射（`OperationException`/`RuleViolation` → 400；`PlanBusyException` → 409）；可选 Step 失败写入 `diagnostics`，其暂存写入丢弃；Step 超时按失败处理（`plan.timeout`）。
- `Ew4.Service/Host/GameOperationExecutor`：`Plane=Plan` 交给 `PlanRunner`。`OperationExecutionPlane.Runtime`、`RuntimeDispatchTarget`、`ClassifiedRuntimeOperation`、`IRuntimeOperationDispatch`、`RuntimeOperationDispatch`、`IRuntimeGateway`、`RuntimeGateway` 已删除。
- 已迁移的 Request：
  - `play.action`：`play.require-body` → `play.require-revision` → `play.apply-action`（资源键 `session`）。
  - `editor.campaign.create|get|put|edit|roundtrip|btl-to-json`：`editor.parse-request` → 对应 Step（资源键 `workspace:{id}`；`btl-to-json` 无资源键）。
  - `editor.commander.list|skills|get|edit`：`editor.parse-request` → `preview.ensure-binding` → 对应 Step（资源键 `binding`）。
  - `editor.preview.enter|refresh|exit|status`：`editor.parse-request` → 对应 Step（资源键 `binding`）。
- 命名：`Ew4.Platform.Runtime` 更名为 **`Ew4.Platform.Play`**，只保留 `Match/`（FlowQueue、MatchFlow、UndoSlot）。`HostApp`、`ContentBinding*`、`IRuntimeHost`、`IHostActivationListener`、`EditorRuntimeJson`、Editor DI 扩展移到 `Ew4.Service/Host`。

**与上文草案的差异**

- **响应契约暂不变**：已迁移终端都写 `PlanKeys.Response`（整份 `OperationResponse`，槽位 `$response`），原样返回，HTTP 形状与迁移前完全一致。按槽位合成 JSON（`view`/`effects`/`diagnostics`）的路径已实现并有测试，供新终端（如开局 + 音乐）使用。
- `StepSpec` 不含 `Required`：必需/可选由 Plan 条目决定。
- Step 目前放在 `Ew4.Service/Plans/`（`EditorPlans`、`PlayPlans`），是对既有 Interface 的薄封装；"模块自己拥有 Step"留到 Editor/Content 内部拆分时再做。
- `Replace` 冲突策略、`Undo` 暂未实现。
- `OperationClassifier` 与 `OperationExecutionPlane` 已删除；`GameOperationExecutor` 只区分静态资源与 Plan。`PlayDescriptors.Describe` 仅负责 Backend 组装 play descriptor。

**下一步**

1. 迁 `start` / `load`（§8 的 14 个 Step），拆 `PlaySession.Start/Load`；同时加 Audio 模块验证"加音乐不改旧码"。
2. 迁 Persistence（save/saves）、Content（catalog、HQ、商店）、Platform mod 面板、workspace 文件/校验。
3. （已完成）删除 `OperationClassifier` 与 `GameOperationExecutor` 里的旧分派。

**补充（第二批）**：`OperationExecutionPlane.Persistence` 与 `ClassifiedPersistenceOperation` 已删除；所有 play route 统一为 `ClassifiedPlanOperation`，Request 名为 `play.<route>`，资源键 `session`。`start`/`load` 内部（`PlaySession.Start/Load`）尚未拆成 §8 的细粒度 Step，需要 Audio 等新模块时再拆。

**补充（音频）**

- 两类声音分开：**战斗音效**由前端按战斗时间轴（`effects.json` 的 `sound` 字段）与动画同步播放，不循环，后端不参与；**背景音乐**由后端选曲、前端循环播放。
- `play.start` / `play.load` 的 Plan：`play.require-body` → `play.require-revision` → `play.start|load`（写 `Envelope`）→ `audio.pick-bgm`（**可选**）→ `play.compose-response`（把 `data.audio.bgm = { track, loop }` 并入信封；没有曲目时为 `null`）。
- 选曲表是 mod 资源 `client/music.json`（`BgmCatalog`）：玩家国家（取自响应里 `playerFaction` 对应势力的 `country`，退回 `startCountry`）→ 曲目，缺省用 `default`。曲目文件随 mod 走，经 `/content/<track>` 提供。
- 为了让"合成响应"能消费可选 Step 的产物，`StepSpec` 新增 `OptionalReads`：可能不存在的输入，必须用 `TryGet` 读取；注册时仍检查上游确有 Step（含可选 Step）会写它。
- **玩家可配置（按势力）**：选曲顺序为 玩家该国家 → mod 该国家 → 玩家默认 → mod 默认。玩家配置存在存档根目录 `music.json`（与 mod 曲目表同格式）。接口 `play.music`（route `music`）：`{op:"get"}` 返回 `{mod, user, tracks}`；`{op:"set", country, track}` 设置（`country` 省略 = 玩家默认，`track` 为 null = 清除），曲目必须存在于当前 mod 的 `client/` 下。
- **条件 Step**：`PlanBuilder.Step(step, optional, when)`；谓词在该 Step 的读权限内求值，为 false 则跳过（不记 diagnostics）；可能被跳过的 Step 的写入等同可选写入，下游需用 `OptionalReads`。
- 前端 `music.js`：`rpc` 收到 `audio` 就 `play`，同一曲目不重启；离局 `stop`；设置里的音乐音量实时生效；浏览器拦截自动播放时，在下一次点击后重试。
- 本地 `mods/base` 是生成物（git 忽略）；音乐源文件同时放在 `data/client/music*`，重新 `import-base` 不会丢。

**补充（start / load 已拆成 Step）**

- `PlaySession` 改为 `partial`，开局/读档的各阶段在 `PlaySession.Steps.cs`：计算类（`CheckUnlocked`、`OpenStart`、`DecodeSlot`）只读内容与账号；落地类（`InstallStarted`、`InstallLoaded`）是替换会话状态的唯一提交点。旧的私有 `Start`/`Load` 按同样顺序调用它们，两条路径共用一份实现。
- `play.start`：`parse-command` → `validate-local-selection` → `end-battle-profile` → `check-unlocked` → `open-start` → `install-session` → `autosave` → `render-view` → `audio.pick-bgm`（可选）→ `compose-response`。
- `play.load`：`parse-command` → `end-battle-profile` → `read-slot` → `decode-slot` → `install-session` → `autosave` → `render-view` → `audio.pick-bgm`（可选）→ `compose-response`。
- Step 在会话锁内通过 `PlayHost.WithSession` 访问 `PlaySession`；Plan 的资源键 `session` 保证同一时刻只有一个游玩请求。
- 游玩接口的可预期失败（`RuleViolation`、IO、JSON、参数等）仍然是 HTTP 200 + `{ok:false,code,message}`：Step 里转成 `PlayFailureException`，由 Plan 的新能力 `PlanBuilder.OnFailure` 转成该信封。其他路径（未同步内容等）仍按原样上抛。
- 与 §8 草稿的差异：没有单列 Content 的 `build-start`/`apply-jurisdiction`/`apply-victory`（它们仍在 `PlayContent.Start` 内部），也没有单列 `commit-profile`（并入 `install-session`），因为 `Adopt`/`SettleBattle` 依赖阻塞队列，必须与账号写入、换 game 在同一提交点内完成。
