# 开发规范

状态:已实现（与当前 `src/` 对齐）

仓库布局与名词见根 [README.md](../../README.md)、[glossary.md](../glossary.md)。依赖分层见 [architecture/01-overview.md](../architecture/01-overview.md)。

## 目标框架

| 项目 | TargetFramework |
|------|-----------------|
| `Ew4.Platform.Engine` | `netstandard2.1`（零 NuGet 依赖） |
| 其它库 / Host.Web / Cli | `net10.0` |

`LangVersion 12`、`Nullable enable`、`TreatWarningsAsErrors`（`src/Directory.Build.props`）。库项目不用 `ImplicitUsings`。

## 依赖规则

- **`Ew4.Platform.Engine` 不引用**任何其它项目或 NuGet 包。
- 方向：Content 不引用 Session/Host；Authoring 不引用 Session（详见 architecture/01 §4）。
- 新类型放在正确层，禁止「下沉」规避依赖。

## 代码与 API

- 命名：PascalCase 类型/成员；接口 `I` 前缀；`Async` 后缀。
- 单文件单主类型；文件作用域命名空间。
- 规则/数据错误 → `RuleViolation`（`area.reason`）；HTTP Play API → `{ ok, code, message }`。
- 提交前：`scripts/ew4.sh fmt`（CI：`fmt --check`）。

## 测试

- 已有：`tests/Ew4.Test.*` 与**目标** `src` 同名（Content、Session、Editor 等，见 `tests/README.md`）。
- 改代码后至少：`scripts/ew4.sh build`、`scripts/ew4.sh test`；内容相关可加 `validate smoke`。
- 后续可加 `tests/Ew4.Architecture.Tests` 锁依赖图（尚未实现）。

## 分支与提交

- 分支：`refactor/pack-architecture`；封存标签：`legacy/0.2.12-sealed`。
- 提交信息：`范围: 说明`（如 `content:`、`host:`、`docs:`）。
- 设计变更：先改 `docs/` 再改代码；声明式 Mod 规则见 [rule-extensions-without-lua.md](rule-extensions-without-lua.md)。
