# C# → Java 迁移路线（草案）

状态：**规划**（未启动实现）  
最后更新：2026-10-27  

本文描述若将 **EW4 rewrite 服务端与平台库** 迁到 Java 时，如何按现有分层落地、如何改写法、如何测、容器要什么。**不修改** [冻结架构](../../arch/frozen/layering.md) 的语义，只讨论「另一套语言实现同一依赖图」。

## 1. 范围与目标

### 1.1 在范围内

| 模块（对应 `src/`） | Java 侧建议产物 | 说明 |
|---------------------|-----------------|------|
| `Ew4.Interface` | `ew4-interface`（纯 API jar） | 操作描述符、Plan 键、Host 契约；无 IO |
| `Ew4.Platform.Engine` | `ew4-engine` | 规则与局内状态；**最先迁、测试最密** |
| `Ew4.Platform.Ai` | `ew4-ai` | 依赖 Engine |
| `Ew4.Platform.Persistence` | `ew4-persistence` | 唯一磁盘边界 |
| `Ew4.Platform.Mod` | `ew4-mod` | 合并、BTL、安装守卫 |
| `Ew4.Platform.Content` | `ew4-content` | 静态表、目录、开局 |
| `Ew4.Platform.Play` / `Editor` | `ew4-play` / `ew4-editor` | 与 C# 同级、互不引用 |
| `Ew4.Service` | `ew4-service` | Plan 注册表与 `PlanRunner` |
| `Ew4.Backend.Web` | `ew4-backend`（Spring Boot 或等价） | HTTP + 静态 `wwwroot` |
| `Ew4.Tool` | `ew4-tool`（CLI） | 可与 Service 共用库 |

### 1.2 明确不在「语言迁移」里一次性解决

- **`Ew4.Front.Web/wwwroot`**：仍是同一份 Web 前端；Backend 继续 `GET /` 提供静态资源即可（与现网一致）。
- **Android 路线 A**（[android-shell.md](./android-shell.md)）：当前是 **Java 壳 + 内嵌 .NET 子进程**。若后端改为 Java，需另立 **Android 路线**（例如壳内起 JVM 进程或 JNI），**不是**本文默认验收；默认可继续 **侧载 .NET APK**，Java 后端先在桌面/CI 验证。
- **自动转译器（cs2j 等）**：不采用；按模块 **手写 + 测试对齐**。

### 1.3 成功标准（总）

1. **依赖图**与 C# 一致（可用 ArchUnit 等锁定，见 §6）。
2. **对外契约不变**：HTTP 路径、JSON 形状、`format`/`version`/存档指纹与 C# 一致（除非单独版本化）。
3. **关键黄金用例**：Engine 单测 + 存档 roundtrip + BTL roundtrip（有语料时）与 C# 输出可比。

---

## 2. 迁移顺序（按依赖图）

与 [layering.md](../../arch/frozen/layering.md) 一致，**禁止**跳层引用。

```text
阶段 0  契约与基础设施（Interface、哈希、JSON 选项、异常码）
阶段 1  Engine + Ai（无 IO）
阶段 2  Persistence（序列化、路径抽象）
阶段 3  Mod（二进制 BTL）+ Content（静态表）
阶段 4  Play、Editor
阶段 5  Service（Plan 注册表）
阶段 6  Backend.Web + Tool
阶段 7  （可选）Android 与新宿主集成
```

每阶段验收：**该层 Java 测试全绿** + **与 C# 同层测试用例语义对齐**（§6）。

---

## 3. 语言与 API 对照（怎么改才「Java 够用」）

### 3.1 `record` + `with`（Engine 核心）

**C# 现状**：`EngineCommand`、`WorldState`、`Combatant` 等不可变；`unit with { ... }` 非破坏性更新。

**Java 做法**（推荐组合）：

| 策略 | 适用类型 | 实现 |
|------|----------|------|
| `record` + 窄 `withXxx` | 命令、小 DTO | `MoveCommand.withDestination(int cell)` 返回新 record |
| 分层更新 | `GameSession` / `WorldState` | 只 `withWorld(World w)`，避免一次改 30 个字段 |
| Builder（生成） | 字段极多的构造 | Lombok `@Builder` 或小型 codegen；**仅构造**，运行期仍不可变 |
| 禁止 | 可变实体 + 共享引用 | 与「revision + Commit」模型冲突 |

**目的对齐**：每次命令产生新 `SessionChange` / `WorldChange`，旧状态不被原地修改。

### 3.2 LINQ

**C#**：`.Where` / `.Select` / `.ToArray` / `.GroupBy` 等。

**Java**：`Stream` + `collect(Collectors.toList())`；注意：

- 空集合用 `List.of()` / `Collections.emptyList()`，与 C# `Array.Empty<T>()` 对齐。
- 需要稳定顺序时显式 `sorted(Comparator...)`（与 C# `OrderBy` 对齐处写注释或对照测试）。

### 3.3 可空引用 `string?`

**Java**：`Optional` 仅用于返回值；字段用 `@Nullable` + 约定；**契约测试**覆盖「缺字段拒载」与 C# 一致。

### 3.4 `async` / `await`（仅 Backend）

**C#**：`PlayEndpoints` 等 `await reader.ReadToEndAsync()`，进入 `game.Invoke` 前读完 body。

**Java**：

- **推荐**：Spring MVC / Javalin **同步**读 body，再调用同步 `PlanRunner.run(...)`（与现 C# `PlanRunner.Run` 一致）。
- **可选**：Java 21 **虚拟线程** + 同步写法，降低阻塞平台线程的担忧。
- **不需要**在 Engine/Service 引入 CompletableFuture 链。

### 3.5 `ReadOnlySpan<byte>` / `stackalloc` / `u8`（Content、Mod）

**C#**：切片读 little-endian、BTL 行按偏移解码、`stackalloc byte[4]`。

**Java**：

```text
优先：byte[] raw + int offset + int length（或 ByteBuffer.wrap(raw).order(LITTLE_ENDIAN)）
可接受：Arrays.copyOfRange 复制小片段（单行记录几十字节级）
不必：为 Span 引入 Netty，除非 profiling 证明热点在拷贝
常量："YSAE" → byte[] 或 StandardCharsets.UTF_8
```

**Sha256Hash**：JVM/Android 用 `MessageDigest.getInstance("SHA-256")`；**无需** C# 的「无 OpenSSL 托管回退」。仍须保证与 C# **同一输入字节** 得 **同一 hex**（跨平台存档指纹）；保留与 `Sha256HashTests` 等价的 Java 测试。

### 3.6 `System.Text.Json`

**Java**：Jackson（或 Gson）；须显式配置：

- camelCase 命名（对齐 `WorldSave.Options`）。
- 枚举字符串、禁止未知字段（对齐 `UnmappedMemberHandling.Disallow`）。
- `JsonElement` 式松散 JSON → `JsonNode`；手写写出 → `JsonGenerator`。

**验收**：Persistence / Mod 的 roundtrip 测试与 C# 字节或 canonical JSON 比较。

### 3.7 委托与命令分发

**C#**：`EngineCommandDispatcher` + `Dictionary<Type, EngineCommandHandler>`。

**Java**：`Map<Class<? extends EngineCommand>, Handler>` 或 **sealed interface `EngineCommand` + switch**（Java 21+）。注册在启动期完成，与 C# `Register<T>` 等价。

### 3.8 Plan 与 `Func<>`

**C#**：`PlanStep(..., Func<PlanContext, bool>? When)`。

**Java**：`Predicate<PlanContext>` / `Function<OperationPayload, String>`；`PlanContext` 类型化键可用 `PlanKey<T>` record + 运行时 `Class<T>` 校验（对齐 `PlanContext.Get<T>` 抛错语义）。

### 3.9 其它 C# 语法

| C# | Java |
|----|------|
| `checked` 算术 | `Math.addExact` / `multiplyExact` 或显式范围检查 |
| `yield return` 迭代路径 | `Stream` 或 `Iterator` |
| 主构造函数、集合表达式 `[]` | 普通构造器、`List.of()` |
| 扩展方法 `MapPlayApi(this WebApplication)` | 静态 `PlayEndpoints.mapPlayApi(app)` |
| `partial class` | 拆文件或代码生成 |

### 3.10 Engine 层保持的约束（迁 Java 后仍遵守）

- 无文件 IO、无系统时钟/随机隐式来源（由调用方注入）。
- 哈希统一走与 C# 契约一致的 SHA-256 API（Java 实现名可不同，结果必须一致）。

---

## 4. 仓库与构建布局（建议）

与 `src/` 镜像，便于对照：

```text
java/
  ew4-interface/
  ew4-engine/
  ew4-ai/
  ...
  ew4-backend/
  pom.xml 或 build.gradle.kts（多模块）
```

- **包名**：例如 `org.ew4.engine`（与 Android 壳 `org.ew4` 可共存，模块不同）。
- **版本**：与 C# `save version`、`format` 字符串同表维护（文档 + 测试常量）。
- **C# 保留期**：双栈并行直到 Java 通过 §6 门禁；不删除 C# 直至维护者宣布切换。

---

## 5. 容器与运行环境

### 5.1 当前 C# 开发镜像（对照）

`ew4-dev:local`（见 `.config/docker/Dockerfile.dev`）：

- .NET SDK 10.0  
- OpenJDK 17（Android apk / apktool）  
- Android SDK build-tools 34  

命令入口：`scripts/ew4.sh`（build / test / web / apk）。

### 5.2 Java 迁移期建议环境

| 组件 | 版本建议 | 用途 |
|------|----------|------|
| **JDK** | 21 LTS（最低 17，若用 sealed switch 倾向 21） | 编译、测试、Backend |
| **构建** | Maven 3.9+ 或 Gradle 8+ | 多模块 |
| **测试** | JUnit 5 | 对齐 xUnit |
| **架构测试** | ArchUnit | 对齐 `Ew4.Test.Architecture` |
| **JSON** | Jackson 2.x | 序列化 |
| **HTTP** | Spring Boot 3.x（Jakarta）或轻量 Javalin | 替代 ASP.NET Core |

### 5.3 Docker 策略（二选一或并存）

**A. 扩展 `ew4-dev:local`（推荐过渡期）**

- 在现有 Dockerfile.dev 增加：`openjdk-21-jdk-headless`、Maven（或只挂载 wrapper）。
- 单容器内可同时 `scripts/ew4.sh test` 与 `./mvnw test`（CI 一条流水线对比两边）。

**B. 独立 `ew4-java-dev` 镜像**

- 仅 JDK + Maven + Node（若前端构建需要）+ 复制 `mods/` 语料挂载。
- 不负责 Android apk；apk 仍用现镜像。

**环境变量**（与 C# 对齐处需在 Java Backend 文档列出）：

- `EW4_HOME` / install 布局：仍由 Persistence 抽象，**Java 实现读同一目录结构**。
- `EW4_ACCESS_TOKEN`：Android 本机访问时 cookie 校验（Java 中间件等价实现）。
- `EW4_FORCE_MANAGED_SHA256`：Java 侧可忽略或仅测试用（无此平台问题）。

### 5.4 本地非 Docker

允许，但 CI 应以 Docker 为准；与 [ew4-dev skill](../../../.cursor/skills/ew4-dev/SKILL.md) 一致时，**.NET 仍只通过 `scripts/ew4.sh`**，Java 可增加 `scripts/ew4-java.sh`（实现阶段再建）。

---

## 6. 测试策略

### 6.1 分层对齐（镜像 `tests/README.md`）

| C# 测试项目 | Java 模块测试 | 优先级 |
|-------------|---------------|--------|
| `Ew4.Test.Platform.Engine` | `ew4-engine` | P0 |
| `Ew4.Test.Interface` | `ew4-interface` | P0 |
| `Ew4.Test.Platform.Persistence` | `ew4-persistence` | P0 |
| `Ew4.Test.Platform.Mod` | `ew4-mod` | P1 |
| `Ew4.Test.Platform.Content` | `ew4-content` | P1 |
| `Ew4.Test.Service` | `ew4-service` | P1 |
| `Ew4.Test.Platform.Play` | `ew4-play` | P2 |
| `Ew4.Test.Editor` | `ew4-editor` | P2 |
| `Ew4.Test.Tool` | `ew4-tool` | P2 |
| `Ew4.Test.Architecture` | ArchUnit | P1（每阶段更新允许边） |

### 6.2 三类验证

1. **移植单测**：按 C# 测试类逐用例翻译（Engine、Sha256、SlotSaveEnvelope、ModInstallGuard…）。
2. **黄金文件**：  
   - 存档：`Encode(world)` 的 JSON 或外层信封 + `sha256` 与 C# 一致（或 canonical 比较）。  
   - BTL：`mods/base/source/original/*.btl` roundtrip 字节一致（与 `ScenarioEditToolkitTests` 同语料；缺失则 `@Disabled`）。  
3. **黑盒 HTTP**（Backend 就绪后）：对同一 `EW4_HOME` 与 mod 集，抽样 `POST /api/play/...` 响应 JSON 与 C# 一致（可脚本 diff）。

### 6.3 双栈 CI 门禁（建议）

```text
job csharp:  scripts/ew4.sh test
job java:    mvn -q test
job compare: （可选）仅 Engine + Persistence 的固定 fixture 向量比较
```

不在第一阶段追求「全仓库 HTTP 对比」，先 **Engine + 存档 + 哈希**。

### 6.4 Android 相关测试

- Java 后端 **不替代** 现 `scripts/ew4.sh apk` 真机流程，除非 Android 壳改为起 JVM。
- 迁移期：Android 仍测 .NET APK；Java Backend 用桌面 `web` 或容器端口测。

---

## 7. 分阶段验收清单

| 阶段 | 交付 | 验收 |
|------|------|------|
| 0 | Interface + SHA-256 + 共享错误码 | Java 单测 = C# `Sha256HashTests` / 契约常量 |
| 1 | Engine + Ai | `Ew4.Test.Platform.Engine` 用例全集通过（或列明已知差异表） |
| 2 | Persistence | `SlotSaveEnvelopeTests`、WorldSave v4 roundtrip |
| 3 | Mod + Content | BTL roundtrip（语料存在）；静态表读盘向量 |
| 4 | Play + Editor | 编辑器 workspace 冒烟用例子集 |
| 5 | Service | `PlanTests` / ProfileStore 等价 |
| 6 | Backend + Tool | `scripts/ew4.sh web` 与 Java 端口并行 smoke；Tool 导出 APK 可仍走 C# 直至 Tool 迁移 |
| 7 | Android（可选） | 新壳文档 + 真机探针等价 `--probe` |

---

## 8. 风险与决策点

| 风险 | 缓解 |
|------|------|
| `with` 机械量大 | 代码生成、分层 record、先迁命令与核心状态 |
| JSON 细微差异 | canonical 比较、禁止 float 非规范表示 |
| 双栈维护成本 | 阶段 1–2 完成后才扩 Backend；C# 为权威直至门禁通过 |
| Android 嵌 .NET | Java 后端不自动解决；需单独路线（进程模型、targetSdk、体积） |

**维护者需拍板**：

- Java Backend 是否与 C# **长期双栈**还是 **切换后下线 C#**。  
- Android 是否从「嵌 Mono」改为「嵌 JVM」。  
- 是否允许迁移期暂时放宽 ArchUnit（应禁止，除非改冻结文档）。

---

## 9. 相关文档

- 分层：[layering.md](../../arch/frozen/layering.md)  
- Android 现状：[android-shell.md](./android-shell.md)  
- 测试索引：[tests/README.md](../../../tests/README.md)  
- 开发镜像：`.config/docker/Dockerfile.dev`  

完成实现后：摘要写入 [done/](../done/)，若契约变更再走冻结 arch 流程；本文可标为已完成或归档。
