# Ew4.Platform.Session

一局游戏、局外账户与存读档。见 [docs/architecture/04-session-play-host.md](../../docs/architecture/04-session-play-host.md)。

## 已实现

- `SessionService`：绑定单个 `ResolvedContent`，`Catalog` / `Start` / `Apply` / `Leave`（`Start` 依赖 P2 `IScenarioBuilder`）
- `AccountService`：占位
