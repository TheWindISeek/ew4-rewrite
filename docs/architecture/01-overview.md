# 01 总体架构

状态:草案

## 1. 要解决的问题

旧实现里,规则解析、Mod 安装、整局玩法、局外养成、UI 宿主缠在一起。几个具体症状:

| 症状 | 位置(旧代码) | 后果 |
|---|---|---|
| `ModStore` 同时是 Mod 管理器、会话宿主和 API 入口 | `Ew4.Mods/ModStore.cs` | 只想打包 Mod 的工具也要带整个游戏 |
| `Mods → Client` 依赖 | `Ew4.Mods.csproj` | 校验"能开局"必须链接整个 Client |
| `ClientContent` 一个构造函数加载一切 | `Ew4.Service/ClientContent.cs` | 内容加载与展示数据混杂 |
| "Legacy" 项目装了三种东西 | `Ew4.Legacy` | 新的扩展 API 被命名成"旧" |
| 内容路径白名单有 4 到 5 份 | `ModFiles`、`Web/Program.cs`、`LogicRevision`... | 新增一类内容要改很多处 |
| 宿主按子系统选规则 | `ClientGame.Action` 的 `switch` | Core 内部划分泄露给宿主 |

## 2. 设计原则

1. **原版就是一个 Mod。** 内置内容是 id 为 `base` 的 Pack,与 Mod 同格式、同加载路径。
2. **逻辑与数据分离。** 规则逻辑在 Core(C#),可变的内容在 Pack(JSON)。同一份数据通过迁移器适配不同引擎版本。
3. **JSON 是唯一真相。** 引擎运行时只读 JSON。BTL/XML 只经导入/导出转换,不作为运行时数据源。
4. **依赖只向下。** 上层可以依赖下层,反之不行。
5. **同一时间只有一个激活的 Pack。** 不做多 Mod 叠加。
6. **宁可拒绝,不要静默出错。** 版本不匹配的存档或数据明确拒绝并说明原因。

## 3. 分层

```text
┌──────────────────────────────────────────────────────┐
│ Host            Web / Android                         │
│                 进程、路由、安全策略、文件路径、切换 Pack  │
├───────────────────────────┬──────────────────────────┤
│ PlayApi (Client)          │ AuthoringApi             │
│ 前端 ↔ 一局游戏            │ 前端 ↔ 编辑器             │
├───────────────────────────┼──────────────────────────┤
│ Session                   │ Authoring                │
│ 一局状态、账户、存读档      │ 草稿包、编辑、预览         │
├───────────────────────────┴──────────────────────────┤
│ Content                                              │
│   Packs        读取目录/zip、清单、哈希                 │
│   Schema       内容模型、schema 版本、迁移器            │
│   Validators   校验管线                                │
│   Importers    BTL/XML/BIN → JSON                    │
│   Exporters    JSON → BTL/XML                        │
│   Builder      剧本定义 + 规则 → 初始 WorldState        │
├──────────────────────────────────────────────────────┤
│ Core            IEngine / RuleSet / WorldState       │
└──────────────────────────────────────────────────────┘
```

## 4. 依赖规则

| 模块 | 可以依赖 | 不可以依赖 |
|---|---|---|
| Core | (无) | 一切其他模块;不做文件和网络 IO |
| Content | Core | Session、Authoring、PlayApi、Host |
| Session | Core、Content | Authoring、PlayApi、Host |
| Authoring | Core、Content | Session、PlayApi、Host |
| PlayApi | Session(间接 Content) | Authoring、Host |
| AuthoringApi | Authoring | Session、PlayApi、Host |
| Host | 以上全部 | (最外层) |

关键推论:

- **Session 与 Authoring 互不依赖。** 预览需要一局游戏时,由 Host 组合二者,Authoring 自己不创建 Session。
- **Content 不知道谁在使用它。** 校验"能否开局"通过 Core 的 `IEngine.NewGame` 完成,不经过 Session。
- 这些规则由架构测试强制执行(见 [开发规范](../development/conventions.md#5-架构测试))。

## 5. 模块职责

| 模块 | 负责 | 不负责 |
|---|---|---|
| **Core** | 规则计算、状态转移、AI 决策 | 读文件、知道 Pack、知道 JSON schema |
| **Content** | 把"一个目录"变成"校验过的、Core 可用的内容",并提供格式转换 | 会话、网络、UI |
| **Session** | 持有一局和局外账户,存读档,绑定指纹 | Pack 导入、渲染 |
| **PlayApi** | 前端请求 ↔ 会话命令,状态 → 视图模型 | 内容加载 |
| **Authoring** | 草稿包的创建/复制/编辑/校验/导出 | 玩家存档 |
| **Host** | 进程、HTTP/WebView 桥、路径、安全限制、激活 Pack | 业务规则 |

## 6. 旧代码到新模块的映射

| 旧项目 / 类 | 去向 | 做法 |
|---|---|---|
| `Ew4.Platform.Engine` | `src/Ew4.Platform.Engine` | **原样复制**(已完成),随后加 `IEngine` 门面 |
| `Ew4.Legacy` 原格式读取 | `Ew4.Platform.Content`:Importers | 重写为导入器,保留解析逻辑可复用部分 |
| `Ew4.Legacy` `ExtensionLoader` | `Ew4.Platform.Content`:扩展 JSON 加载 + Core 声明式规则 | 不新增 Lua;见 [ADR-0008](../adr/0008-declarative-rules-instead-of-lua.md) |
| `Ew4.Legacy` `LegacyScenarioHost` | `Ew4.Platform.Content`:Builder | 输入改为 JSON 内容模型 |
| `Ew4.Service` `ClientContent` | `Ew4.Platform.Content`(加载)+ `Ew4.PlayApi`(展示数据) | 拆开 |
| `Ew4.Service` `ClientGame` | `Ew4.Platform.Session` + `Ew4.PlayApi` | 拆开 |
| `Ew4.Persistence` | `Ew4.Platform.Session`(或其子目录) | 合并,加存档元数据 |
| `Ew4.Mods` / `ModStore` | `Ew4.Platform.Content`(包管理)+ `Ew4.Platform.Runtime`(激活) | 拆开 |
| `Ew4.ModTool` / `ModChecks` | `Ew4.Cli` / 测试 | 基于新 Content 重写 |
| `Ew4.Backend.Web` / `Ew4.Android` | `Ew4.Platform.Runtime.Web` / `Ew4.Platform.Runtime.Android` | 变薄;前端 `wwwroot` 先沿用 |
| `Ew4.Verify` | 拆入各模块的测试项目 | 保留检查逻辑,逐步迁移 |

详细迁移步骤见 [07-migration-plan.md](07-migration-plan.md)。
