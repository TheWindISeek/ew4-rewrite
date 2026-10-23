# 11 做减法、安装布局与 Mod 包规范

状态:草案(§1.1 已确认项可照此实施;§1.2 其余改名、§12 Q3–Q7 仍待拍板)  
日期:2026-10-22(§8.5、D7/D8、D9–D11 于 2026-10-23 对账)  
范围:分支 `refactor/pack-architecture` 的下一阶段。**本文只规定"做什么、为什么、做到什么算完成",具体实现由开发者执行。**

## 0. 为什么要有这一份

第一轮重构按"旧代码封存、新代码在 `src/`"的计划做完后,实际状态是:

- `src/` 里新旧两套并存,旧栈的代码还在新栈里又复制了一份。
- 运行时数据(`mods/`、`.ew4-host/`)散在源码树根目录,路径逻辑靠多处回退和一堆环境变量拼成。
- 项目命名没有按职责取(`Host` 与 `Host.Web`、`PlayApi` 里装着 `Client/`、`Content/Build` 里装着 `Legacy*`)。
- "JSON 为主"只是外壳:`mods/base/scenarios/*.json` 只是指向 BTL 的索引桩。

**本阶段的唯一目标是把整体复杂度控制住。** 所有取舍都按这条判断:能删的先删,能合并的先合并,不新增概念。

## 1. 决策一览

### 1.1 已确认(来自你的答复)

| # | 决策 | 影响 |
|---|---|---|
| D1 | **应用内不再支持 APK 导入** | 整个 `Ew4.Mods` 删除;`AssetRipper`、`Stb*` 依赖随之消失 |
| D2 | **Mod 以 JSON 表达;BTL 由引擎内部生成,不进包** | Mod 作者和 zip 包里只有 JSON;见 §5 |
| D3 | **Mod 允许只写差异;`parent` 可以指向任意已安装的包**(`base`、`huangkai`……) | 见 §4.3 |
| D4 | **`Core` 改名为 `Engine`** | 见 §7 |
| D5 | **Android 暂停** | 只保留一条约束:路径只通过"安装根"取得,将来换根即可 |
| D6 | 第 5 条是笔误 | 无 |
| D7 | **内容包不进 Git;发行默认带 Mod,玩家也可从文件导入** | 见 §8.5。维护者用种子数据构建 `base` 并打进发行物;玩家平时只玩安装根下的包,也可导入 `.zip`。不支持 APK。`install/mods/` 与 `install/user/` 一律不纳入源码仓库管理 |
| D8 | **删除 Lua 与 API 1,扩展只走声明式(API 2)** | 见 §6.3。规则路径统一,旧 API 1 + Lua 的 Mod 不再支持 |
| D9 | **`Ew4.PlayApi` 改名为 `Ew4.Service`**(游戏**服务层**,对前端暴露游玩/Mod 等能力) | 见 §7。须先删旧 `Ew4.Service` 项目再改名 |
| D10 | **前端与宿主分开命名** | `Ew4.Front.Web`(浏览器 UI)、将来 `Ew4.Front.Apk`(Android 壳,D5 暂停期间仅占位);`Ew4.Backend.Web` 只做 ASP.NET **宿主**(Kestrel、路由、静态文件发布),不混叫 Host.Web |
| D11 | **层间接口集中在一个项目 `Ew4.Interface`**（原 `Ew4.Contracts`） | 见 §3.4。跨层只依赖 Interface 里的类型,禁止上层直接引用下层实现细节 |

### 1.2 本文新增的建议

| # | 建议 | 状态 |
|---|---|---|
| R1 | `parent` 只允许**一层**:被指向的包自己不能再有 `parent` | 采纳 |
| R2 | 删除 Lua 运行时和 `MoonSharp` 依赖,只支持声明式扩展(API 2) | **已确认(D8)** |
| R3 | `pack.json` 删除 `capabilities` 字段 | 采纳 |
| R4 | 第一批只把**剧本**做成 JSON;XML 规则表整文件作为资源保留 | 采纳 |
| R5 | 其余项目改名(§7)与 §7.3 内部命名 | **已完成** |
| R6 | 发行包默认内置 `base` 等 Mod;玩家亦可导入 zip(§8.5) | **已确认(D7)** |

## 2. 设计原则(控制复杂度)

1. **一个概念一个落点。** 路径只有一个类提供;剧本只有一种格式;格式转换只有一个入口;**跨层契约只出现在 `Ew4.Interface`**。
2. **运行时数据不进源码树。** 编译产物在 `build/`,可运行的安装目录在 `install/`,二者都不进 Git。
3. **删除优先于重构。** 先删掉没人用的,再改名、再整理。不给马上要删的代码改名。
4. **Mod 的可改范围先收窄。** 只做 Mod 的切换与基础数据(剧本、规则参数、文本),不做地图、将领图片、兵种的创作入口。
5. **每一步有门禁。** 删除或改名之后,必须证明行为没有变(§9)。

## 3. 目标架构

### 3.1 项目(`src/` 下实现 + `Ew4.Interface`,另加测试)

| 项目(目标名) | 现名 | 职责 | 依赖 |
|---|---|---|---|
| `Ew4.Interface` | `Ew4.Contracts` | **层间契约**;命名空间 `Interface.Front` / `Interface.Service` 等 | (无) |
| `Ew4.Platform.Engine` | `Ew4.Engine` | **平台层**:规则引擎 | Interface |
| `Ew4.Platform.Content` | `Ew4.Content` | **平台层**:Pack、校验、剧本构建、Original 导入 | Platform.Engine、Interface |
| `Ew4.Platform.Session` | `Ew4.Session` | **平台层**:局、账户、存档 | Platform.Engine、Platform.Content、Interface |
| `Ew4.Platform.Runtime` | `Ew4.Runtime` | **平台层**:安装根、Pack 激活/导入 | 上列 Platform + Interface |
| `Ew4.Service` | `Ew4.Client` | **服务层**:游玩/Mod 编排;实现 `Interface.Front` | Platform.*、Interface |
| `Ew4.Editor` | `Ew4.Editor` | 创作:剧本/工作区 | Platform.Content、Interface |
| `Ew4.Front.Web` | `Host.Web/wwwroot` | **表现层**:静态 UI | (无 Service 引用) |
| `Ew4.Backend.Web` | `Ew4.Web` | **接入层**:Kestrel、`/api/*`、DI、发布 `Front.Web` | Service、Editor、Platform.*、`Front.Web` |
| `Ew4.Front.Apk` | (将来) | Android 壳 + WebView;D5 暂停 | 同 Front 契约 |
| `Ew4.Tool` | `Ew4.Cli` | 离线 CLI | Platform.Content、Editor、Platform.Runtime |
| `tests/Ew4.Test.*` | 与目标 `src` 同名（如 `Ew4.Test.Editor`） | 单元与门禁 | 见 `tests/README.md`；`src` 未改名前 csproj 引用可仍指向旧项目 |

**被删除(历史):** 旧 `Ew4.Client` 项目、`Ew4.Legacy`、`Ew4.Mods` 等。

术语对照:**表现层** = `Ew4.Front.*`;**接入层** = `Ew4.Backend.Web`;**服务层** = `Ew4.Service`;**平台层** = `Ew4.Platform.*`。

> `Runtime` 是否并入 `Web`:见 §12 Q3。`Ew4.Interface` 保持单包,不按层再拆程序集。

### 3.2 依赖方向

```text
                    Ew4.Interface
                          ↑
Platform.Engine ← Platform.Content ← Platform.Session ← Platform.Runtime ← Service ← Backend.Web
                ↖                                              ↗
                  Editor ──────────────────────────────────────
Tool → Platform.Content、Editor、Platform.Runtime

Ew4.Front.Web  ──HTTP──►  Backend.Web → Service (实现 Interface)
```

只能沿实现层向左依赖;**跨层调用只通过 `Ew4.Interface` 中的类型**(或 HTTP +json,其形状与 Interface 中 DTO 一致)。禁止 `Front.Web` 引用 `Ew4.Service` 程序集;禁止 `Service` 向路由层以外暴露 `Platform.Content` 内部类型。

### 3.3 目录与命名规则

- 目录名 = 程序集名 = 根命名空间(`Ew4.Front.Web` 可为静态资源项目,命名空间可有可无)。
- 子命名空间只按功能划分;`PlayApi/Client/*` 合并进 `Ew4.Service` 后不再保留 `Client` 子目录名(避免与项目名混淆)。
- 术语:**"原版格式"**指 BTL/XML/BIN 这类原游戏数据格式,代码里叫 `Original`;**不再使用 `Legacy` 一词**作为项目名、命名空间或目录名。

### 3.4 `Ew4.Interface`(D11)

**目的:** 前端、服务层、运行时、引擎之间的边界清晰,又不为本阶段再拆一堆小实现包。

| 命名空间(示例) | 放什么 | 谁实现 / 谁消费 |
|---|---|---|
| `Ew4.Interface.Front` | 游玩、Mod 列表/导入、账户等 **HTTP 契约**(路由名、请求/响应 DTO、错误码);与现有 `POST /api/{route}?q=…` 对齐 | `Ew4.Service` 实现;`Ew4.Front.Web` 与 `Ew4.Backend.Web` 按契约序列化 |
| `Ew4.Interface.Service` | 服务层对 Runtime/Session 的门面接口(如 `ISessionService`、`IPackActivation` 等,从各项目收口到此) | `Ew4.Service` / `Ew4.Platform.Runtime` / `Ew4.Platform.Session` 实现 |
| `Ew4.Interface.Engine` | 若需与 `IEngine` 解耦,可在此声明引擎门面;**初版可仍让 `IEngine` 留在 Engine** | `Ew4.Platform.Engine` 实现 |

规则:

1. `Ew4.Interface` **不引用**任何其它 `Ew4.*` 项目;只允许 BCL 与极少量共享注解包。
2. 新增跨层能力时:**先在 Interface 加接口或 DTO,再在实现项目落地**;禁止在 `Backend.Web` 路由里手写与前端重复的匿名 JSON 形状。
3. 迁移可以渐进:先把对外路由相关类型迁入 `Interface.Front`,再收 Session/Runtime 接口;**不要求一步搬空 `IEngine`**。
4. 单元测试可对 Interface 做序列化/兼容性测试,无需拉起 Web。

## 4. Mod 包规范

### 4.1 包形态

- **只有一种分发格式:`.zip`。** 去掉 `.ew4mod` 别名。
- `pack.json` 必须在 zip 的**根部**,不能嵌在子目录里。
- 导入和导出都是 zip。导出就是把包目录原样压缩。

目录结构(目标):

```text
<包根>/
  pack.json
  scenarios/<剧本id>.json        剧本(完整 ScenarioDocument),唯一的剧本格式
  rules/**/*.json                规则参数(声明式扩展)
  text/**/*.json                 文本
  assets/**                      资源(图片、音频、XML 规则表等),整文件覆盖
```

不再有:`source/original/*.btl`(BTL 不进包)、`scenarios/*.ew4scenario.json`(与索引 JSON 合并成一种)。

### 4.2 `pack.json`(目标)

```json
{
  "schema": 1,
  "id": "my-mod",
  "name": "我的 Mod",
  "version": "1.0.0",
  "parent": "base",
  "requires": { "engine": ">=0.3.0 <0.4.0" },
  "formats": { "scenario": 2, "rules": 1, "text": 1, "asset": 1 }
}
```

| 字段 | 必填 | 含义 |
|---|---|---|
| `schema` | 是 | `pack.json` 自身的格式版本 |
| `id` | 是 | 包标识:小写字母、数字、`.`、`-`、`_`,1 到 64 位。`base` 保留 |
| `name`、`version` | 是 | 显示用 |
| `parent` | 否 | 这个包"在谁的基础上改"。不写表示自包含 |
| `requires.engine` | 是 | **适配的引擎版本范围**。引擎版本不在范围内,安装和启用都拒绝 |
| `formats` | 是 | 每类数据所用的格式版本。引擎不认识的版本,拒绝并说明是哪一类 |

- `capabilities` 字段**删除**(R3)。现在它只用来校验 `rule-api-2` 这一个值,没有第二个取值时它只是多余的概念。以后真有第二套能力时再加回来。
- `formats` 只列**包里实际出现**的数据类。

### 4.3 `parent` 规则

| # | 规则 |
|---|---|
| P1 | `parent` 可以是**任何已安装的包**,不限于 `base` |
| P2 | `parent` 指向的包自己**不能再有 `parent`**(R1)。因此叠加最多一层,不存在链和环 |
| P3 | 叠加方式是**整文件覆盖**:Mod 里有的文件替代 `parent` 的同路径文件;没有的从 `parent` 取。**不支持"删除 parent 的某个文件"** |
| P4 | 安装时 `parent` 必须已安装,否则**拒绝**并说明缺的是哪个包 |
| P5 | 删除一个包时,如果有其他包把它当 `parent`,**拒绝**并列出这些包 |
| P6 | Mod 与其 `parent` 的 `formats` 中,同一类数据的版本必须一致,否则拒绝(避免两种格式混在一起) |
| P7 | 两个包都必须满足 `requires.engine` |
| P8 | `base` 不可删除,也不可被同 id 的导入覆盖 |

现有代码已支持"`parent` 指向任意已安装包"(`HostApp.Activate` 里按 `manifest.parent` 打开);**缺的是 P2、P4–P7 这些检查**,需要在安装时补上。

### 4.4 导入流程

```text
选择 .zip
  ① 读取 pack.json(失败:不是内容包)
  ② 检查 requires.engine、formats、id(P7、P8)
  ③ 检查 parent 已安装且自包含(P2、P4、P6)
  ④ 校验:Reference 级;对"本包新增或改动的剧本"做 SmokeStart
  ⑤ 全部通过才复制到 <安装根>/mods/<id>/,否则不落盘
  ⑥ 用户选择"启用"
```

- 启用时仍做校验,但只需 Schema 级(第④步已经证明可开局)。
- 失败信息要带**错误码和文件路径**,能让作者定位。
- 现状:安装时没有 SmokeStart,启用时也只检查 Schema 级,存在"能导入、不能开局"的空档,上面的流程补上它。

### 4.5 导出

- 编辑器里的草稿包导出 = 把包目录压缩成 zip,保持差异形态(有 `parent` 就仍然有)。
- 不提供"合并 parent 后导出完整包"。需要时作者自己复制。

## 5. JSON 与 BTL:Mod 读写 JSON,引擎内部生成 BTL(D2)

### 5.1 现状

- 当前有**两种**剧本 JSON:索引桩 `scenarios/{id}.json`(只指向 BTL)和编辑用 `scenarios/{id}.ew4scenario.json`(完整内容)。
- BTL 作为"编译产物"存放在包里的 `source/original/*.btl`,运行时 `ScenarioJsonBuilder` 读它开局。
- 格式转换的代码分散在 `Content/Importers/`、`Content/Import/`、`Content/ScenarioEdit/` 三处。

### 5.2 目标

| 项 | 规定 |
|---|---|
| 剧本格式 | **只有一种**:`scenarios/{id}.json`,即完整的 `ScenarioDocument`。Mod 作者和编辑器只面对它 |
| BTL | **不进包**。由引擎在解析内容时从 JSON 生成,字节交给现有的 BTL 读取与开局流程 |
| 生成入口 | **只有一处**:`ScenarioBtlCodec`。导入、导出、编辑保存、命令行往返、运行时开局都只调它,禁止另写一套拼字节 |
| 缓存 | 生成结果按"剧本 + 内容指纹"缓存。默认内存;需要时落到 `<安装根>/user/cache/`。**按需生成**(开某个剧本时才生成它),不在启用时一次性编译全部 |
| 未知字节 | 沿用现有规则:已理解的层用有名字的字段;其余部分(未知段、扩展尾)以命名的二进制块保留,编辑器不可改 |
| 范围 | 本阶段**只做剧本**。XML 规则表(`def_*.xml`)、`stringtable`、`catalog.json` 等仍作为 `assets/**` 整文件资源,由 Mod 整文件覆盖(R4) |

说明:"运行时先生成 BTL 字节、再按旧方式读回"是刻意的取舍。这样复用已验证的开局路径,不必重写读取器。以后需要时,再让开局直接使用 `ScenarioDocument`。

### 5.3 `base` 的转换

- 一次性命令 `Tool import-base`:从原版数据生成 `mods/base`,剧本转为 JSON,不再复制 `.btl`。
- **验收标准:对全部 95 个原版剧本,`JSON → BTL` 生成的字节与原 BTL 逐字节一致。** 不一致的差异逐条记录并解释,不通过则不允许删除"包内存放 BTL"的旧路径。

### 5.4 对既有设计的修订

| 既有表述 | 修订为 |
|---|---|
| 03 §6 / 旧 ADR-0003:"引擎运行时只读 JSON,BTL 是产物" | 引擎对外只读 JSON;**BTL 是内部中间表示,不进包、不对外暴露** |
| 08 §1:"Pack 内 `source/original/*.btl` 是编译产物,由 Exporter 写出" | 包里没有 BTL;导出器只用于验收往返和(可选)兼容旧工具 |
| 08 §2:索引 JSON 与编辑 JSON 两种 | 合并为一种 |

## 6. 做减法清单

### 6.1 删除整个项目

| 项目 | 理由 | 前置条件 |
|---|---|---|
| `Ew4.Mods` | 旧宿主 `ModStore` + APK 导入 + 图像解码。D1 后没有存活功能 | 先移除 `Host.Web` 对它的引用(`ApkPackImportRunner`) |
| `Ew4.Service`(旧) | `PlayApi/Client/*` 里有同名副本 | 确认新栈不引用它(目前只被 `Mods` 引用) |
| `Ew4.Legacy` | `Content/Build/Legacy*` 里有同名副本 | 同上 |
| `Ew4.Persistence` | `Session/Persistence/*` 里有副本 | 同上 |
| `Ew4.ModChecks` | 检查对象是旧 `ModStore` 与 APK | 随 `Mods` 删除;有价值的检查项先转成 `tests/` 里的用例 |
| `Ew4.EastAsiaMapVerify` | 地图质检脚本,与引擎无关 | 移出 `src/` 与 `Ew4.sln`(放到 `maps/` 的工具目录或直接删除) |

### 6.2 删除项目内的残留

| 位置 | 内容 |
|---|---|
| `Host.Web` | `ApkPackImportRunner.cs`、`LegacyModPackConverter.cs`;`PlayModImportStore` 中 `PackImportKind.LegacyArchive` 分支;`PackImportProbe` 中识别旧存档包的部分;`wwwroot` 的 `index.html`、`mods.js` 里与 APK 相关的文案和逻辑(共 8 处引用,实施时用 `grep -i apk` 逐一处理) |
| `Content` | `RepositoryLayout`(被 `InstallLayout` 取代,见 §8);其中东亚/民国路径、`ModChecksProjectPath`、旧目录回退全部删除 |
| `Host.Web` | `LocalRuntimePaths` 及 `AuthorHostOptions` 里的路径逻辑(被 `InstallLayout` 取代) |
| `scripts/` | 见 `scripts/README.md`（`dev/paths.sh`、`docker.sh`、`cli.sh` 等） |
| `.gitignore` | `/local/**`、`/packs/`、`/workspaces/`、`/.ew4-host/` 等旧路径条目 |
| `Ew4.sln` | 被删项目的条目 |
| `docs/` | `architecture/10-runtime-assets-and-import.md` 中的 APK 内容;各文档里对 `Ew4.Mods`、`ModChecks`、`Legacy` 的描述(见 §10) |

### 6.3 Lua 与 API 1(已确认,D8)

- 现状:`ExtensionScriptPolicy` 已规定"API 2 及以上的扩展包禁止 Lua;API 1 仅保留只读兼容的 `ExtensionScriptRuntime`"。`Ew4.Platform.Content` 仍引用 `MoonSharp`。
- 决定:**删除 `ExtensionScriptRuntime`、`ExtensionScriptPolicy` 中的 Lua 与 API 1 分支、`MoonSharp` 引用,以及 `Engine` 中仅为 Lua 存在的 `IRuleScriptRuntime` 注入点**(删除前确认无其他用途)。安装与启用只接受声明式规则(`formats.rules` 对应 API 2)。
- 影响:依赖 API 1 或 Lua 的旧 Mod 无法加载;校验失败时明确提示需要声明式规则包。与 D1(无 APK 入口)一致,不再保留第二条兼容路径。
- 收益:规则加载逻辑单一,少一个第三方依赖与安全面。

### 6.4 删除前的自检

每删一个项目前,确认下面都没有残留引用:

- `src/**/*.csproj` 的 `ProjectReference`
- `Ew4.sln`
- `scripts/**`、`.config/docker/Dockerfile`
- `using Ew4.<被删名>`
- `docs/**` 中的链接

## 7. 命名

### 7.1 项目重命名

| 现名 | 目标名 | 状态 |
|---|---|---|
| `Ew4.Core` | `Ew4.Platform.Engine` | **已确认(R5)** |
| `Ew4.PlayApi` | `Ew4.Service` | **已确认(D9)** |
| `Ew4.Contracts` | `Ew4.Interface` | **已确认(D11)** |
| `Host.Web/wwwroot` | `Ew4.Front.Web` | **已确认(D10)** |
| `Ew4.Platform.Runtime.Web`(宿主部分) | `Ew4.Backend.Web` | **已确认(D10)** |
| `(将来)` | `Ew4.Front.Apk` | 占位(D5 暂停) |
| `Ew4.Authoring` | `Ew4.Editor` | **已确认(R5)** |
| `Ew4.Host` | `Ew4.Platform.Runtime` | **已确认(R5)** |
| `Ew4.Cli` | `Ew4.Tool` | **已确认(R5)** |
| `Ew4.Platform.Content`、`Ew4.Platform.Session` | 不变 | |
| `tests/Ew4.Test.Editor` | 已用目标名；引用 `Ew4.Editor` | ✅ |

### 7.2 随之要改的地方

改名是机械操作,但覆盖面广,逐项过一遍:

- 目录、`.csproj` 文件名、`Ew4.sln`、`ProjectReference`
- 根命名空间与所有 `using`
- `Ew4.Platform.Engine` 的 `AssemblyInfo.cs` 里的 `InternalsVisibleTo`
- `scripts/ew4.sh`、`scripts/dev/*.sh` 里写死的项目路径(例如 `src/Ew4.Backend.Web/Ew4.Backend.Web.csproj`、`src/Ew4.Tool/Ew4.Tool.csproj`)
- `.config/docker/Dockerfile`、`.config/docker/README.md`
- `Directory.Build.props` 用的是 `$(MSBuildProjectName)`,不用改;但旧的 `build/Ew4.*` 目录需要清理
- 文档中的项目名

### 7.3 内部命名

| 现状 | 目标 |
|---|---|
| `Content/Build/Legacy*` 类与 `Ew4.Platform.Content.Build` 命名空间 | 命名空间 `Ew4.Platform.Content.Original`;类名 `OriginalX` | ✅ |
| `PlayApi/Client/*` 子目录 | 文件直接放入 `Ew4.Service` 根;命名空间 `Ew4.Service` | ✅(S4) |
| 包内 `assets/legacy/` | 运行时表与 catalog → `assets/tables`(读路径兼容 `assets/legacy`) | ✅ |
| 包内 `assets/original/` | 保持 |

### 7.4 做法

1. 必须**在删除旧栈之后**做。
2. 一次改名一次提交,只做 `git mv` 和文本替换,**不夹带任何逻辑改动**。
3. 每次提交后跑 §9 的门禁。

### 7.5 `Ew4.Front.*` 与「服务 / 宿主」

| 名字 | 是什么 | 不是什么 |
|---|---|---|
| `Ew4.Front.Web` | 浏览器里的页面、脚本、样式;只认 HTTP API | 不是 Kestrel、不是 `/api` 实现 |
| `Ew4.Front.Apk` | (将来)Android 壳 + WebView,同样只认 HTTP/契约 | 不是 APK 内容导入;D5 暂停,本阶段不进 sln |
| `Ew4.Backend.Web` | 进程入口:挂路由、静态文件、DI 组装 | **不叫** `Host.Web`,也**不叫** Front |
| `Ew4.Service` | **服务层**:实现 `Interface.Front` / `Interface.Service` | 不是浏览器前端 |

前端项目统一用 **`Ew4.Front.<平台>`**;服务端实现与引擎之间仍用 **`Ew4.Interface`** 单包收口(§3.4),本阶段不为每层再拆一个「仅接口」程序集。

## 8. 安装布局

### 8.1 三个目录的分工

| 目录 | 内容 | 进 Git? |
|---|---|---|
| `build/` | 编译中间产物与输出(`bin`/`obj`) | 否 |
| `install/` | **可直接运行的安装目录**,包含程序、内置数据、已安装的 Mod、用户数据 | 否 |
| 源码树其余部分 | `src/`、`tests/`、`docs/`、`scripts/` | 是 |

**源码树里不再出现任何运行时数据**:根目录的 `mods/`、`.ew4-host/` 都去掉。

### 8.2 安装根布局

```text
install/                       ← 安装根(--home 或 EW4_HOME)
  app/                         发布产物:宿主程序与 wwwroot 前端
  mods/                        所有内容包
    base/                      原版(含地图等数据)
    <id>/                      导入的 Mod(zip 解压)
  user/                        用户数据
    active.json                当前启用的包 id
    saves/<packId>/<指纹>/     存档
    workspaces/<id>/           编辑器草稿
    cache/                     可丢弃的缓存(如 BTL 生成结果)
    logs/
    tmp/                       导入时的临时文件
```

### 8.3 路径规则

- **只有一个类 `InstallLayout(root)`** 提供全部路径:`Mods`、`User`、`Saves`、`Workspaces`、`Cache`、`Logs`、`Tmp`、`ActiveFile`、`AppRoot`。所有项目只通过它取路径。
- **安装根的确定顺序:** 命令行 `--home` → 环境变量 `EW4_HOME` → 可执行文件所在目录。**仅此三项**。
- 删除:`EW4_MODS_DIR`、`EW4_PACKS_DIR`、`EW4_STATE_DIR`、`EW4_WORKSPACES_DIR`、`EW4_LOCAL_ROOT`、`EW4_BUILD_ROOT`、`EW4_INSTALL_ROOT`、`EW4_IMPORT_SEED` 等,以及所有"依次尝试旧目录"的回退逻辑。
- 浏览器前端由 `Ew4.Front.Web` 构建/拷贝,随 `Ew4.Backend.Web` 发布到 `install/app/`;开发时也从安装目录读,**不再从 `Ew4.Backend.Web` 项目内嵌的 `wwwroot` 混放**。
- 原版数据种子(`data/import-seed`,不进 Git)只用于 `Tool import-base`,路径由命令行参数传入,**不写进路径类**。
- 策划数据目录 `maps/` 不属于引擎,与 `InstallLayout` 无关,其去留不在本阶段决定。

### 8.4 开发流程

```text
scripts/ew4.sh build       编译到 build/
scripts/ew4.sh install     发布到 install/app;若 install/mods/base 不存在,则提示或运行 import-base
scripts/ew4.sh web         = install,然后以 install/ 为安装根运行
```

- 开发与发布使用**同一套目录**,不另设"开发路径"。
- Android 以后只需把安装根换成应用私有目录,布局相同(D5)。
- 旧的 `.ew4-host/`、根目录 `mods/` 里的内容属于开发数据,**一次性手动搬到 `install/` 下即可,不写迁移脚本**。

### 8.5 玩家如何得到内容包(D7)

内容与源码**分轨管理**:

| 角色 | 做什么 | 落在哪 |
|---|---|---|
| 维护者 / CI | 用不进 Git 的种子数据运行 `Tool import-base`,生成 `base`;打发行包时把默认 Mod(至少 `base`)放进 `install/mods/` | 发行物里的 `install/` 布局,不进 Git |
| 玩家(常见) | 解压或安装维护者发布的包,直接玩自带的 `base` 等 | `<安装根>/mods/` |
| 玩家(可选) | 在应用内选择**文件**导入符合 §4 的 `.zip` 内容包(与导出格式相同) | 解压到 `<安装根>/mods/<id>/`,走 §4.4 校验 |
| 开发者 | 本地 `ew4.sh install` 后若缺少 `base`,自行跑 `import-base` 或从别处拷贝已生成的 `mods/base` | 同上 |

约定:

- **原版游戏数据不进 Git**;构建 `base` 只在维护者/开发者环境完成,版权与分发边界由发行方负责。
- **应用内不再提供 APK 导入**;玩家扩充内容只通过 zip,不提供"从安装包解包原版"的向导。
- `install/mods/`、`install/user/` 视为**运行时数据**,与 `build/` 一样不提交;文档与 Agent 约束里不再把根目录 `mods/` 当作源码树的一部分。

## 9. 回归门禁

旧的 `Ew4.Verify`(三十余万项检查)已不在仓库中,目前只有 3 个测试项目、约 260 行。删除与改名阶段 `Engine` 的算法不变,风险主要在**接线**。门禁因此集中在接线与开局上:

| # | 门禁 | 命令/方式 | 通过标准 |
|---|---|---|---|
| G1 | 编译 | `scripts/ew4.sh build` | 零错误零警告(`TreatWarningsAsErrors`) |
| G2 | 单元测试 | `scripts/ew4.sh test` | 全绿 |
| G3 | 全剧本开局 | `scripts/ew4.sh validate smoke` | 对 `base` 的所有剧本 SmokeStart 通过 |
| G4 | BTL 往返 | `scripts/ew4.sh check btl` | 通过 |
| G5 | 浏览器烟测 | `scripts/ew4.sh web` 后手动:开局、走一回合、存档、读档、切换到另一个包再切回 | 无报错 |
| G6(§5 完成后新增) | JSON→BTL 字节一致 | 新命令 | 95 个剧本全部一致 |

- **S0 先记录基线**:在没改动任何东西的状态下跑一遍 G1–G5,保存输出(通过的剧本数、测试数)。之后每步都与基线比较。
- 已知不足:`Engine` 的行为快照测试很薄。本阶段不改 `Engine`,所以不是阻塞项;但**之后任何触及 `Engine` 的改动前**,应先从标签 `legacy/0.2.12-sealed` 里的旧 `Ew4.Verify` 迁入对应检查。

## 10. 执行顺序

| 步骤 | 内容 | 完成标准 |
|---|---|---|
| **S0** | 记录基线(§9) | 基线输出已保存 |
| **S1** | 切断 APK:删除 `Host.Web` 的 APK 与旧包分支、前端相关文案 | `Host.Web` 不再引用 `Ew4.Mods`;G1–G5 与基线一致 |
| **S2** | 删除旧栈:`Mods`、`Client`(旧)、`Legacy`、`Persistence`、`ModChecks`、`EastAsiaMapVerify`;同时删除 Lua 与 API 1(D8) | `Ew4.sln` 与 `src/` 中不再有这些项目;§6.4 自检通过;G1–G5 与基线一致 |
| **S3** | 安装布局:实现 `InstallLayout`,替换 `RepositoryLayout`/`LocalRuntimePaths`,删环境变量与回退;改 `scripts`、Dockerfile;搬走根目录 `mods/`、`.ew4-host/` | 仓库根不再有运行时数据;在 `install/` 下完整跑通 G5 |
| **S4** | 新建 `Ew4.Interface` 并收口层间类型;重命名(§7),含 `PlayApi`→`Client`、`wwwroot`→`Front.Web`、`Host.Web`→`Web`,一次一提交 | 每次提交后 G1–G5 通过;`Front.Web` 不引用 `Client` 程序集 |
| **S5** | 剧本只有 JSON:合并两种剧本 JSON,实现内部 BTL 生成与缓存,收敛转换入口,删除包内 BTL | G6 通过;G3 通过 |
| **S6** | 包规范落地:`pack.json` 精简(R3)、`parent` 规则 P1–P8、安装校验(§4.4)、zip 导入/导出统一 | 针对 P1–P8 每一条有拒绝路径的测试;G5 中"导入一个差异 Mod 并启用"通过 |
| **S7** | 文档对账 | §11 列出的文档已更新或删除 |

- S1 与 S2 之所以先于布局与改名:删除之后要改的东西少得多。
- 每一步单独提交,出问题时可以按步回退。
- S5 与 S6 可以互换,但都必须在 S4 之后,避免对旧名字写新代码。

## 11. 本文会让哪些文档过时

实施时需要更新或删除:

| 文档 | 需要改的地方 |
|---|---|
| `README.md`(根) | 目录说明、启动方式、`install/` 布局 |
| `docs/README.md`、`docs/library.md` | 索引加入本文 |
| `docs/development/todo.md` | 进度与"给 Agent 的约束"(`mods/` 路径、`Ew4.Platform.Runtime.Web` 名称) |
| `docs/development/environment.md` | 目录、环境变量 |
| `docs/architecture/01-overview.md` | 项目表、依赖表、旧→新映射(旧栈已删,映射不再需要) |
| `docs/architecture/02-core-contract.md` | `Core` → `Engine`;若删 Lua,去掉脚本运行时注入 |
| `docs/architecture/03-content-and-packs.md` | §1 目录布局与 `pack.json`、§6 格式转换、§8 脚本 |
| `docs/architecture/04-session-play-host.md` | `Host` → `Runtime`、`PlayApi` → `Client`,存档与目录布局 |
| `docs/architecture/05-authoring.md` | `Authoring` → `Editor` |
| `docs/architecture/08-scenario-edit-pipeline.md` | §1、§2:BTL 不进包、剧本 JSON 合并为一种 |
| `docs/architecture/10-runtime-assets-and-import.md` | 应用内无 APK;维护 `mods/`、运行时 `install/mods/`;Tool 离线导入 |
| `docs/development/rule-extensions-without-lua.md` | 若删除 Lua,说明 API 1 不再支持 |
| `docs/development/conventions.md` | 项目名、目录结构 |
| `docs/glossary.md` | 增加 Original、安装根;去掉 Legacy |

## 12. 未决问题

| # | 问题 | 选项 | 状态 |
|---|---|---|---|
| Q1 | **玩家怎么得到 `base`?** | 见 §8.5 | **已决(D7)**:发行默认带 Mod;玩家可 zip 导入;种子数据与 `install/mods` 不进 Git |
| Q2 | 是否删除 Lua 与 API 1 | 删 / 保留只读兼容 | **已决(D8)**:删,只保留声明式 API 2 |
| Q3 | `Runtime` 是否并入 `Web` | 并 / 不并 | 先不并,看 `Tool` 是否需要它 |
| Q4 | 安装时对"改动的剧本"做 SmokeStart,是否会太慢 | 做 / 只做 Reference 级 | 先做;若实测超过可接受时间再降级,并把耗时写进文档 |
| Q5 | 剧本 BTL 的生成缓存是否需要落盘 | 只内存 / 落盘到 `user/cache` | 先只内存;实测启动变慢再落盘 |
| Q6 | `maps/`(东亚、民国的策划数据)的去留 | 留 / 移出 / 删 | 不在本阶段决定,只保证它不再出现在 `InstallLayout` 与任何路径常量里 |
| Q7 | 旧 Mod(皇凯等)怎么转成新 zip | 一次性离线工具放 `tools/`,不进 `Ew4.sln` / 不转换 | 视你是否还需要这批 Mod。D1 只说应用内不再导入 |

## 13. 完成的定义

本阶段结束时应满足:

1. `src/` 下只有 §3.1 列出的项目(含 `Ew4.Interface`、`Ew4.Front.Web`;不含暂停的 `Front.Apk`);`Ew4.sln` 与之一致。
2. 仓库根 **`mods/`** 仅作内容包维护（不进 Git，除 `mods/README.md`）；运行时读 **`install/mods/`**（由脚本从 `mods/` 拷贝）。无 `.ew4-host/`；在 `install/` 下可完整运行。
3. 路径只来自 `InstallLayout`;环境变量只剩 `EW4_HOME`。
4. 全库不再出现 `Legacy` 作为项目名、命名空间或目录名(包内数据目录同样处理)。
5. 剧本在包里只有 JSON 一种形态;95 个剧本 `JSON→BTL` 字节一致。
6. 安装 zip 时 P1–P8 都有拒绝路径与测试;`pack.json` 声明适配的引擎范围。
7. §9 的 G1–G6 全部通过,且与 S0 基线一致。
8. §11 所列文档已对账。
9. `Ew4.Front.Web` 工程不引用 `Ew4.Service` 等实现项目;跨层类型集中在 `Ew4.Interface`,`Ew4.Backend.Web` 路由与 JSON 形状与 `Interface.Front` 一致。
