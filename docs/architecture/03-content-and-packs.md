# 03 内容包与 Content 层

状态:草案

Content 层负责把"一个目录里的文件"变成"Core 能直接使用的、校验过的内容"。原版(`base`)和 Mod 走完全相同的路径。

## 1. Pack(内容包)

### 1.1 目录布局(草案)

```text
mods/
  base/                       原版,随发行版提供,只读
    pack.json
    scenarios/                剧本定义(JSON)
    maps/                     地图(引用已有地图 id;新地图见 §7)
    rules/                    规则参数(JSON)
    commanders/               将领定义
    units/                    兵种定义
    countries/                国家/势力
    text/                     字符串表(多语言)
    mod.json                  可选:剧本存储策略（legacy-btl / embedded-json）；见 08
    rewrite/                  可选:extension.json（声明式扩展）
    assets/                   图片、动画、音频、字体；tables 与 original 等运行时资源
    source/                   可选:legacy BTL 语料（嵌入 JSON 后仍可作 G6 参考）
  my-mod/
    pack.json
    ...                       与 base 同结构;可以只放被修改的文件
```

具体目录名在 P0 评审时敲定。**目录由 ContentKind 注册表定义**(见 §3),不在宿主代码里写死。

### 1.2 `pack.json`(草案)

```json
{
  "schema": 1,
  "id": "my-mod",
  "name": "我的 Mod",
  "version": "1.0.0",
  "parent": "base",
  "requires": {
    "engine": ">=0.3.0 <0.4.0"
  },
  "formats": {
    "scenario": 2,
    "rules": 1,
    "commander": 1
  }
}
```

| 字段 | 含义 |
|---|---|
| `id` | 小写字母、数字、`.`、`-`、`_`,1 到 64 位;`base` 保留 |
| `parent` | 复制来源。**只用于说明和"只放差异文件"的补全**,不是多包叠加 |
| `requires.engine` | 需要的引擎版本范围 |
| `requires.capabilities` | **已废弃(R3)**：若旧包仍带该字段且非空,引擎会校验;新包勿写 |
| `formats` | 这个包里每类文件使用的 schema 版本。引擎据此运行迁移器 |

### 1.3 单激活与"补全"

- **同一时间只激活一个 Pack。** 不存在多个 Mod 叠加的加载顺序问题。
- 如果 Mod 声明 `parent: "base"` 且缺少某些文件,Content 在**解析时**按路径从 `base` 补全。这是为了让"只放修改过的文件"成为可能,而不是多包叠加。
- 补全结果是整文件级别:Mod 里存在的文件整体替代 base 的同路径文件,不做字段级合并。
- 解析完成后得到的 `ResolvedContent` 不再保留"哪个文件来自哪个包"之外的层次信息。

## 2. 解析流程

```text
Pack(目录/zip)
  │  ① 读取 pack.json,检查 requires / formats
  │  ② 按路径补全(parent)→ 虚拟文件系统 IContentFs
  │  ③ 对每个 ContentKind:读取 → 迁移到当前 schema → 得到模型对象
  │  ④ 校验管线(见 §4)
  │  ⑤ 计算指纹
  ▼
ResolvedContent(不可变)
  │  ⑥ Builder:剧本模型 + 规则 → WorldState / RuleSet
  ▼
交给 Core(IEngine.NewGame)
```

## 3. ContentKind 注册表(取代散落的白名单)

```csharp
// 草案
public interface IContentKind
{
    string Name { get; }                    // 如 "scenario"
    string PathPattern { get; }             // 如 "scenarios/*.json"
    int CurrentSchema { get; }
    bool AffectsFingerprint { get; }        // 是否参与指纹
    ContentLimits Limits { get; }           // 文件大小/数量上限

    object Load(byte[] data, int schema);   // 读取;schema 低于 Current 时先经迁移器升级
    IEnumerable<Diagnostic> Validate(object model, ValidationContext context);
}

public interface IContentKindRegistry
{
    IReadOnlyList<IContentKind> All { get; }
    IContentKind? Match(string logicalPath);   // 路径是否属于某类内容;不属于则拒绝
}
```

**新增一类内容 = 新增一个 `IContentKind` 实现并注册。** 路径白名单、大小上限、是否参与指纹都在这一处声明,宿主和打包器不再各存一份。

## 4. 校验管线

```csharp
// 草案
public enum ValidationLevel { Schema, Reference, Constraint, SmokeStart }

public interface IContentValidator
{
    IReadOnlyList<Diagnostic> Run(ResolvedContent content, ValidationLevel upTo);
}

public sealed record Diagnostic(Severity Severity, string Code, string Path, string Message, string? Hint = null);
```

| 级别 | 检查内容 | 例子 |
|---|---|---|
| `Schema` | 文件结构、字段类型、版本 | 缺字段、类型错 |
| `Reference` | 跨文件引用 | 剧本引用不存在的将领、地图 id |
| `Constraint` | 规则约束 | 地图策略(§7)、单格容量、势力数 |
| `SmokeStart` | 用 `IEngine.NewGame` 真的开一局 | "能打包不能开局"在这里被拦下 |

- 校验**不依赖 Session**,只依赖 Core 和 Content。
- 编辑器保存、导入、启用,都用同一个校验器。每次调用可指定检查到哪一级(编辑中可只检查到 `Reference`,导出前检查到 `SmokeStart`)。
- 诊断信息必须带 `Path` 和 `Code`,能让作者定位到具体文件和字段。

## 5. 指纹

- 指纹 = 对所有 `AffectsFingerprint` 为真的文件按路径排序后,逐个"路径:内容哈希"拼接再哈希。
- 指纹计算基于**源文件字节**,不是迁移后的对象,因此引擎升级本身不改变指纹。
- 存档绑定:`pack.id` + 指纹 + 引擎 `BehaviorVersion`(见 [06](06-versioning-compat.md))。

## 6. 格式转换:Importers 与 Exporters

**方向与角色:**

| 方向 | 角色 | 谁用 |
|---|---|---|
| BTL/XML/BIN → JSON(Importer) | 把原版和旧 Mod 转进新格式 | 构建 `base`;导入旧 `.ew4mod` |
| JSON → BTL/XML(Exporter) | 兼容旧引擎、对外互通、**往返验收** | 编辑器导出;测试 |

**规则:**

1. **运行时只读 JSON。** BTL/XML 是从 JSON "编译"出的产物,不被引擎读取。不允许同时存在两份真相。
2. **JSON 模型保留未知字段。** 已理解的字段用有名字、有类型的属性;尚未理解的部分放入 `extra`(不透明字节块,如 base64),原样保存、原样写回。
   - 好处:编辑已知字段不会丢掉未知部分;往返测试可以成立。
   - 约束:`extra` 只能由 Importer 产生;编辑器不允许修改它。
3. **BTL 自带扩展段机制**(旧 `ModTool` 的 `*-apply` / `*-strip` 已用它写入 JURI/VICT 等段),Exporter 在此基础上实现。

**往返验收标准(编辑器开发的前置门槛):**

| 对象 | 标准 |
|---|---|
| 95 个剧本 BTL | `BTL → JSON → BTL` 字节一致;不一致的差异逐条记录并解释 |
| `def_*.xml` | `XML → JSON → XML` 经规范化后语义一致(属性顺序、缩进不计) |
| 地图 BIN | 不在 P1/P4 范围,见 §7 |

**这套验收通过之前,不开始编辑器的保存功能**,否则作者的编辑可能悄悄丢数据。

## 7. 地图

- 地图是独立的内容类型,与剧本分开。剧本只通过 `mapId` 引用地图。
- **默认路径:** 选择 `base` 已内置的地图 id。
- **进阶路径(后期):** 上传/编辑地图,经过 **MapPolicy** 校验。

MapPolicy 检查项(草案,后续细化):

| 项 | 说明 |
|---|---|
| 尺寸上限 | 宽高、总格数 |
| 地形枚举 | 只允许已注册的地形 id |
| 城市落点 | 必须在陆地,与已有城市不重叠 |
| 引用完整 | 势力、城市、设施引用存在 |
| 可达性 | 各势力出生点在地图上互相可达 |
| 体积预算 | 文件大小、瓦片数量 |

- 校验失败默认**不允许保存或导出**。上传地图走同一条校验管线,没有绕过路径。
- 地图 BIN ↔ JSON 的双向转换比 BTL 风险更高,**单独立项**,排在剧本 JSON 完成之后。

## 8. 规则扩展(声明式,不用 Lua)

- **新架构方向:** Mod 自定义技能、胜负、剧本条件等一律通过 **JSON + Core 声明式能力** 完成,见 [ADR-0008](../adr/0008-declarative-rules-instead-of-lua.md) 与 [rule-extensions-without-lua.md](../development/rule-extensions-without-lua.md)。
- **不引入 MoonSharp 作为长期依赖。** 缺表达能力时扩展 `ExtensionEffect` / 条件类型 / `operation`,在 Core 实现,而不是新增 `.lua`。
- **封存 `0.2.x`:** 旧包可能含 `script`/`function`;迁移期可在 Content 保留只读兼容或导入时改写为 JSON — 不为新功能扩展 Lua API。
- **晋升规则(沿用 ADR-0005 精神):** 反复出现的 Mod 需求 → Core 内置字段 + schema 版本,保证确定性与可测性。

## 9. 公开接口汇总(草案)

```csharp
public interface IPackSource             // 目录或 zip 的统一读取
{
    PackManifest Manifest { get; }
    bool Exists(string path);
    byte[] Read(string path);
    IEnumerable<string> List(string prefix);
}

public interface IPackRepository         // 已安装包的管理(不含会话)
{
    IReadOnlyList<PackInfo> List();
    PackInfo Install(IPackSource source, InstallOptions options);   // 校验失败则不落盘
    void Remove(string packId);
    IPackSource Open(string packId);
}

public interface IContentResolver
{
    ResolvedContent Resolve(IPackSource pack, IPackSource? parent);
}

public interface IPackExporter           // 打成 .ew4mod
{
    void Export(IPackSource pack, Stream destination);
}

public interface IScenarioBuilder
{
    BuiltScenario Build(ResolvedContent content, string scenarioId, ScenarioSelection selection);
    // BuiltScenario = WorldState + RuleSet,可直接交给 IEngine.NewGame
}
```

包管理(`IPackRepository`)**不引用 Session**。这正是旧 `ModStore` 缺的拆分。
