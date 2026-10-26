# Ew4.Platform.Engine

规则内核（`netstandard2.1`）。对外入口：**`IEngine`**；实现按目录分组，命名空间仍为 `Ew4.Platform.Engine`。

| 目录 | 内容 |
|------|------|
| `Core/` | `IEngine`、`Engine`、`RuleSet`、`EngineInfo` |
| `Commands/` | `EngineCommand*`、分发器与 handler |
| `Systems/` | `WorldCommitPipeline` |
| `World/` | 地图、回合、移动、外交、胜利等 |
| `Combat/` | 战斗与结算 |
| `Ai/` | AI 规划与执行 |
| `Scenario/` | 剧本事件与阵营 |
| `Session/` | `GameSession`（战场会话聚合）、`SessionActions`（仅 SyncPlayer / CommitWorld） |
| `Account/` | 账户**状态数据**与不变量（`PlayerAccount`、`OwnedCommander`）；交易/编队/继承/消耗品/酒馆**规则**在 Content `Account/` |
| `Extensions/` | Mod 扩展技能与脚本 |
| `Economy/` | 回合经济、成长、动画等辅助 |
| `Meta/` | 辖区、多人、外观解析 |

设计说明：[docs/arch/module-responsibilities.md](../../docs/arch/module-responsibilities.md)。
