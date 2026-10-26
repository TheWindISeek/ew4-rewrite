# 分层与依赖（冻结）

## 约束

1. **磁盘只经 `Ew4.Platform.Persistence`。** Mod、Content、Engine、Service、Backend 不直接 `File.*`、不持有 install 路径。
2. **规则只在 `Ew4.Platform.Engine`。** 内存演算，无 IO、无 NuGet 依赖，目标框架 `netstandard2.1`。不内置原版数值表，数值由 Content 提供。
3. **HTTP 只在 `Ew4.Backend.Web`。** 它只引用 `Interface`、`Service`、`Front.Web`，**不引用任何 `Ew4.Platform.*`**；所有请求经 `IGameOperations.Invoke`。
4. **Service 是编排层。** 请求到 Step 的对应只在 `Ew4.Service/Plans` 的注册表；Service 不按 HTTP 路径分流。
5. **Play 与 Editor 是同级、互不引用的两个域。** 游玩差异（战役/征服/教程）体现为不同 Plan，不是 `if`。
6. **Interface 不含 IO 与规则**，只有契约与 Plan 基础类型。
7. **单实现**：不为“多种后端”抽 `I*`；Tool 与 Service 共用同一套 Persistence/Mod API。
8. **不下沉**：新类型放进正确层；不得为了绕过依赖把类型挪到更底层。

## 依赖图（测试强制）

```text
Front.Web   (无引用)
Interface   (无引用)
Engine      (无引用、无 NuGet)
Ai          → Engine
Persistence → Interface, Engine
Mod         → Interface, Engine, Persistence
Play        → Engine
Content     → Interface, Ai, Engine, Mod, Persistence
Editor      → Interface, Content, Engine, Mod, Persistence
Service     → Interface, Ai, Content, Editor, Engine, Mod, Persistence, Play
Backend.Web → Interface, Service, Front.Web
Tool        → Content, Editor, Engine, Persistence, Service
```

白名单在 `tests/Ew4.Test.Architecture/DependencyGraphTests.cs`。新增项目或新增引用边都属于改架构。

## 数据流

```text
磁盘 → Persistence → Mod（激活/合并/逻辑路径）→ Content（目录/战役/开局）
     → Engine（Apply）→ Service（持局、投影）→ Backend → Front
```

## 不允许

- 让 Backend / Front 直接引用 Platform 类型。
- 在 Content/Mod/Engine 里读写磁盘或解析 install 路径。
- 在 Engine 里引入时间、随机、IO 的隐式来源（需要随机/时间由调用方传入）。
- 把 HTTP 概念（状态码、路径）带进 Platform。
