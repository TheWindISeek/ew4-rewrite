# Ew4.Platform.Play

游玩域：一局内的流程编排，不含 HTTP、存档文件、Pack 激活。

- `Match/FlowQueue`、`MatchFlow`：局内行动队列与回合流转
- `Match/UndoSlot`：撤销槽

Pack 激活与 workspace 预览（`HostApp`）在 `Ew4.Service/Host`。见 [docs/arch/operation-plans.md](../../docs/arch/operation-plans.md)。
