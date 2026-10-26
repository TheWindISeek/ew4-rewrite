# 开发规范

状态:已实现（与当前 `src/` 对齐）

仓库布局与名词见根 [README.md](../../README.md)、[glossary.md](../glossary.md)。依赖分层见 [arch/module-responsibilities.md](../arch/module-responsibilities.md)。

## 目标框架

| 项目 | TargetFramework |
|------|-----------------|
| `Ew4.Platform.Engine` | `netstandard2.1`（零 NuGet 依赖） |
| 其它库 / Host.Web / Cli | `net10.0` |

`LangVersion 12`、`Nullable enable`、`TreatWarningsAsErrors`（`src/Directory.Build.props`）。库项目不用 `ImplicitUsings`。

## 依赖规则

- **`Ew4.Platform.Engine` 不引用**任何其它项目或 NuGet 包。
- 方向：Content 不引用 `Ew4.Service`；磁盘只经 Persistence（详见 [arch/module-responsibilities.md](../arch/module-responsibilities.md)）。
- 新类型放在正确层，禁止「下沉」规避依赖。

## 代码与 API

- 命名：PascalCase 类型/成员；接口 `I` 前缀；`Async` 后缀。
- 单文件单主类型；文件作用域命名空间。
- 规则/数据错误 → `RuleViolation`（`area.reason`）；HTTP Play API → `{ ok, code, message }`。
- 提交前：`scripts/ew4.sh fmt`（CI：`fmt --check`）。

## mods/ 与示例包

- 运行时大包（`base`、`ww1` 等）不进 Git；**白名单示例**在 `mods/ex-*`，与 `mods/README.md` 同级，见根 `.gitignore`。
- 示例必须能跟 `base` 合并跑通，用 `ExampleModsTests` 等单测验证；不要为示例单独加一次性维护脚本。

## 测试

- 已有：`tests/Ew4.Test.*` 与**目标** `src` 同名（Content、Session、Editor 等，见 `tests/README.md`）。
- 改代码后至少：`scripts/ew4.sh build`、`scripts/ew4.sh test`；内容相关可加 `validate smoke`。
- 后续可加 `tests/Ew4.Architecture.Tests` 锁依赖图（尚未实现）。

## 分支与提交

- 主要开发分支：`refactor/pack-architecture`（或当前默认分支）；封存标签：`legacy/0.2.12-sealed`。
- 设计变更：先改 `docs/` 再改代码；声明式 Mod 规则见 [rule-extensions-without-lua.md](rule-extensions-without-lua.md)。

### Git 提交信息

**标题（第一行）**

```text
[范围]: 简短说明（英文或中文均可，一行说完）
```

常用范围：`refactor`、`docs`、`content`、`service`、`host`、`tool`、`test`、`mods`。

**正文（可选，空行后列表）**

- 用 `- ` 列出本提交包含的几类改动，每条一行，动词开头。
- 只写「为什么 / 做了什么」，不写实现细节堆砌。
- **不要**加 `Co-authored-by`、不要带 Cursor / AI 署名行。

**示例**（与仓库近期风格一致）

```text
[refactor]: refactor dir, compile, docker, docs, mods

- del maps, btl
- new arch for ew4-rewriter
- new compile method
- keep engine but rewrite others
```

```text
[docs]: reorganize docs tree and mods pack spec

- add arch/, task/, debug template; remove architecture/
- root readme: docker compose and dotnet run
- mods readme: pack.json and mod.json layout; plan M0 unify manifest
```
