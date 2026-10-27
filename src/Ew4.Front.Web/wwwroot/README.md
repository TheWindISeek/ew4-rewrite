# 游戏界面程序

此目录保存 HTML、JavaScript、CSS，以及征服分页和城市名 JSON 配置。实际贴图、字库、动画、地图块与声音通过 `/content/` 从当前 Mod/基础 data 读取。

作者修改资源应放在 Mod 的 `client/`，不要直接改这里的程序脚本。仅 `conquest-pages.json`、`city-names.json` 支持经 Mod 的 `frontend/` 覆盖。Mod 不允许注入 JS、CSS 或 HTML。

Mod 入口位于主菜单左下角。此程序与后端及 Android 共用；已发布的移动端安装包需单独更新，电脑源码修改不会自动更新手机端。

## 目录

| 目录 / 文件 | 内容 |
|-------------|------|
| `html/` | `index.html`（主游戏）、`editor.html`（Mod 创作页）；访问 `/` 由后端映射到 `html/index.html` |
| `css/` | 全部样式表 |
| `js/` | 全部脚本（启动、库、各界面逻辑、`app.js` 主流程） |
| `*.json` | `conquest-pages.json`、`city-names.json`（根路径 `/…` 供 API 与前端读取） |
| `*.png` | 少量 UI 位图（如 `disband.png`） |

### 界面与脚本（维护时按屏找文件）

| 界面 | 主要 JS |
|------|---------|
| 主菜单 / 征服剧本 | `app.js`、`menus.js` |
| 战役 | `campaign-page.js` |
| 指挥部 | `hq-page.js`、`commander-workflows.js` |
| 存档弹窗 | `saves-page.js`、`save-format.js` |
| 战场 | `app.js`、`battlefield.js` |
| Mod / 局内编辑 | `mods.js`、`editor-in-game.js` |

脚本加载顺序见 `html/index.html` 底部。

## 前端单元测试

测试代码在仓库 `tests/Ew4.Test.Front/`（与其它 `Ew4.Test.*` 同级）。

```bash
scripts/ew4.sh front-test
```
