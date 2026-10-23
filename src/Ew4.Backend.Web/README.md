# Ew4.Backend.Web

Web 宿主（原 `Ew4.Platform.Runtime.Web`）：Kestrel、`/api/*` 路由、静态文件（开发态读 `Ew4.Front.Web/wwwroot`）。

## 运行

```bash
scripts/ew4.sh web
```

浏览器打开 `http://127.0.0.1:5184/`（需 `install/mods/base/` 完整内容包）。

游玩 API：`POST /api/{route}?q=<json>`（由 `Ew4.Service` 实现）。

创作层：`/api/author/*`（`Ew4.Editor`）。
