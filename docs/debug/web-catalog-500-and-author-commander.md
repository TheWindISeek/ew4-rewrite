# Web 主菜单 catalog 500 与创作端将领联调

| 字段 | 值 |
|------|-----|
| 日期 | 2026-10-23 |
| 状态 | 已修复 |
| 影响 | `ew4 web` 主菜单「连接失败」/ HTTP 500；创作内嵌 UI 将领字段误导 |

## 现象

1. **主菜单**：底部 `连接失败` 或 `Error`，浏览器网络里 `POST /api/catalog` 返回 **500**；`initializeClient` 在拉 catalog 阶段失败，整页无法进入正常菜单。
2. **服务端日志**（典型）：`ContentError`，详情类似 `pack.json: No content kind is registered for this path`（或带其它未注册路径）。
3. **创作 → 将领**：列表能出，但早期 UI 把 XML 里的 `star` 当「星级」展示；技能只显示数字，与指挥部/catalog 语义不一致。

## 调用链（便于对照日志）

```text
前端 rpc('catalog')
  → Backend ClientGame /api/catalog
  → HostApp.ActiveSnapshot（启动时 ActivateStoredOrDefault）
  → ModSnapshotLoader.LoadActivatedView
  → ContentValidator 遍历合并后 Source 下所有路径
```

任意 **Error** 级 `Diagnostic` 都会在 `LoadActivatedView` 里变成 `ContentError` 并冒泡为 500，因此主菜单表现与「当前激活 Mod 能否通过 Schema 校验」强绑定，而不是单纯网络问题。

## 根因

### A. `active.json` 只写了 `packId`

`ActivePackStore.Read` 曾只读 `modId`。用户态或旧工具写入的 `install/web/user/active.json` 若仅有 `packId`，读到的激活 id 为 **null**，行为与预期 Mod 不一致；修复后 **同时兼容** `modId` 与 `packId`，`Write` 时两者一并写入，避免再次分叉。

### B. 合并包里残留的 `pack.json`

Mod 已统一为根目录 `mod.json`，但 overlay/父包目录里仍可能躺着历史 **`pack.json`**（未被删除）。`DirectoryPackSource` 列表会把它当作普通资源文件；`ContentValidator` 对「无注册 kind」的路径报 **Error**，激活/拉 catalog 即失败。

处理：`ModManifestPaths.LegacyModJson` 明确该文件名；校验与指纹扫描时 **跳过** `mod.json` / `pack.json` / `.workspace.json`（manifest 仍由 `PackManifestReader` 按规则读取，不经过 kind 注册表）。

### C. 错误信息缺少路径

`ModSnapshotLoader` 在抛出 `ContentError` 时若 `Diagnostic.Path` 非空，应拼进 detail（`path: message`），否则日志里只有泛化文案，难以定位是哪一个残留文件。

### D. 将领 UI 字段与引擎语义

- 引擎侧「适性」对应 commander 条目里的兵种适性列，**不是** XML 里名为 `star` 的遗留字段（当前玩法未用）。
- 技能在数据里是 **catalog `legacy_id`**（整数），展示应结合 `catalog.json` 解析名称，与指挥部一致。

创作 API 路由与契约见 [operations.md](../arch/operations.md)、[interface-contracts.md](../arch/interface-contracts.md)。

## 修复摘要

| 问题 | 改动要点 |
|------|----------|
| catalog 500 / pack.json | `ContentValidator`、`ContentFingerprint` 跳过 `LegacyModJson`；常量集中在 `PackManifestPaths` |
| 激活 id 读不到 | `ActivePackStore` 兼容 `packId` / `modId` |
| 日志难读 | `ModSnapshotLoader` detail 带 `diagnostic.Path` |
| 将领 UI | `editor-in-game.js` + `/api/editor/.../commanders`：适性列、技能 ID + 名称 |

## 复现与验证

本地（端口以 `scripts/ew4.sh web` 输出为准，示例 5184）：

```bash
scripts/ew4.sh install
scripts/ew4.sh web
curl -sS -X POST 'http://127.0.0.1:5184/api/catalog' -H 'Content-Type: application/json' -d '{}'
```

期望：`200`，JSON 含 `scenarios` 等字段，**非** 500。

切换 Mod 后若再 500：

1. 看响应体 / Kestrel 日志里的 `ContentError` **路径**；
2. 检查 `install/web/user/active.json` 是否含有效 `modId` 或 `packId`；
3. 在对应 Mod 目录（及 merge 后的逻辑列表）搜是否还有应删除的 `pack.json` 或未注册扩展名文件。

创作将领：

- `GET /api/editor/workspaces/{id}/commanders`
- `GET /api/editor/workspaces/{id}/skills`
- `GET /api/editor/workspaces/{id}/commanders/{legacyId}/edit`

## 教训

1. **主菜单 500 先查 catalog 与激活快照**，不要只改前端文案；500 几乎都是 `LoadActivatedView` 校验失败。
2. **Manifest 迁移要扫磁盘残留**：忽略读 manifest ≠ 忽略目录里多出来的 `pack.json` 文件。
3. **用户态 JSON 字段改名要兼容读**：`packId` → `modId` 过渡期双读双写，避免静默回 base。
4. **创作 UI 对齐已有玩法语义**（指挥部、catalog），避免把未接入引擎的 XML 字段当成主编辑项。
