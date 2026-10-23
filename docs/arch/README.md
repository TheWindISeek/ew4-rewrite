# 代码结构与接口

与 `Ew4.sln` 一致；细节以 `src/` 为准。环境命令见 [development/environment.md](../development/environment.md)。

## 解决方案项目（`src/`）

| 项目 | 职责 |
|------|------|
| `Ew4.Interface` | 跨层契约（如 `PlayApiRoutes`） |
| `Ew4.Platform.Engine` | 规则引擎（`IEngine` / `Engine`） |
| `Ew4.Platform.Content` | Pack、`mod.json`、`PackInstallGuard`、`ScenarioBtlResolver`、表与剧本加载 |
| `Ew4.Platform.Session` | 局状态、存读档 |
| `Ew4.Platform.Runtime` | `HostApp`、Pack 激活/导入、`InstallLayout` |
| `Ew4.Service` | 游玩 RPC（`ClientGame`、`ClientContent`、`PlaySessionHost` 使用的服务逻辑） |
| `Ew4.Editor` | 创作（`WorkspaceService`、`ScenarioEditor`） |
| `Ew4.Backend.Web` | HTTP 宿主（Kestrel、路由、静态文件） |
| `Ew4.Front.Web` | 浏览器 UI（`wwwroot`，**不**引用 Service/Platform） |
| `Ew4.Tool` | 离线 CLI（`import-base`、`import-apk-overlay`、`validate` 等） |

测试：`tests/Ew4.Test.Platform.Content`、`Ew4.Test.Platform.Session`、`Ew4.Test.Editor`。

## 依赖方向

```text
Front.Web  ←  Backend.Web  →  Service, Editor
                              ↓
                    Platform.Runtime → Session, Content → Engine
                              ↑
                         Interface（契约）
```

## 运行时（Web）

```text
mods/  ──sync──►  install/web/mods/  （及 install/apk/mods/）
                      │
                 HostApp.Activate
                      │
         PlaySessionHost + ClientContent（当前包）
                      │
              ClientGame.Dispatch  ←  POST /api/{route}
                      │
         wwwroot + GET /content/*（Pack 资源）
```

- **安装根：** `EW4_HOME`，默认 `<仓库>/install/web`；各渠道自有 `mods/`、`user/`（Web 与 APK 不共用）。
- **维护目录：** 仓库根 `mods/`（不进 Git，除 `mods/README.md`）。

## HTTP 接口（摘要）

**游玩**（`PlayEndpoints` → `PlaySessionHost` → `ClientGame`）：

- `POST /api/{route}`，body 多为 JSON，也可用 query `q=`。
- 常见 `route`：`catalog`、`start`、`action`、`state`、`save`、`load`、`headquarters`…（完整列表见 `ClientGame.Dispatch`）。

**Mod**（`PlayModEndpoints`，非 `{route}` 通配）：

- `POST /api/mod-prepare`、`POST /api/mod-import`
- `POST /api/mods`（如 `activate`）

**创作：** `/api/author/*`（`AuthorWorkspaceEndpoints`、`AuthorScenarioEndpoints`）。

**静态资源：** `/content/...` → 当前激活 Pack（`PlayStaticFiles`）。

## 内容包（作者）

维护者规范见 **[mods/README.md](../../mods/README.md)**（单文件 `mod.json` 字段与目录树）。  
**Mod 统一视图（载入 vs 使用、快照与指针）**：[mod-view.md](./mod-view.md)（设计稿，实现前对齐用）。  
清单统一说明：[task/plan/pack-manifest-unify.md](../task/plan/pack-manifest-unify.md)（若存在）。

zip → 当前渠道 `EW4_HOME/mods/`；APK 原料 → `Ew4.Tool import-apk-overlay` → `mods/` 再 `sync-mods`。

## 关键代码入口

| 主题 | 位置 |
|------|------|
| DI 与启动 | `Ew4.Backend.Web/Program.cs` |
| 游玩路由 | `Ew4.Backend.Web/PlayEndpoints.cs` |
| RPC 实现 | `Ew4.Service/ClientGame.cs` |
| 包激活 | `Ew4.Platform.Runtime/HostApp.cs` |
| 路径常量 | `InstallLayout`、`RepositoryPaths` |
