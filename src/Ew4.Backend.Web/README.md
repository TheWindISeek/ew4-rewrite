# Ew4.Backend.Web

Web 宿主：Kestrel、`/api/*` 与静态路径仅经 **`Ew4.Interface` + `IGameOperations`**；wwwroot 由 **`IGameWebPaths`** 解析（实现在 Service）。

## 运行

```bash
scripts/ew4.sh web
```

浏览器打开 `http://127.0.0.1:5184/`（需 `install/web/mods/base/` 完整内容包）。

游玩 API：`POST /api/play/{route}?q=<json>`（兼容 `/api/{route}`；由 `Ew4.Service` 实现）。

创作层：`/api/editor/*` → `IGameOperations.Invoke`（`EditorOperationDescriptor` + `EditorWorkspacePayload`）。
