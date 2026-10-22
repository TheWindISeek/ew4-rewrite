# 本地运行与验证目录

正式源代码按功能归入项目：Mod安装、识别、启用、删除位于`Ew4.Mods`，浏览器管理面板位于`Ew4.Web/wwwroot/mods.js`、`mods.css`及`index.html`；通用验证位于`Ew4.ModChecks/ModDeletionChecks.cs`与`ModIdentityChecks.cs`。这些功能不依赖东亚地图目录。

| 路径 | 用途 |
| --- | --- |
| `../build/item-extensions-20261021/` | 5261物品接口独立预览、测试Mod/档案、示例ew4mod及Web/Android编译输出；验收记录在reports/item-extensions-20261021，未操作玩家档案 |
| `local/mod-management/` | 从地图工作目录迁来的通用Mod管理构建、兼容构建、测试档案与历史日志；不是用户发行包 |
| `local/eastasia-preview/server/` | 5190预览使用的旧兼容引擎，保留原存档指纹 |
| `local/eastasia-preview/data/` | 5190的基础资源快照，保留其已有Mod的资源指纹 |
| `local/eastasia-preview/mods/` | 5190已安装的Mod及启用记录 |
| `local/eastasia-preview/saves/` | 5190原账户及战局；已整体迁移，勿与其他运行包混用 |
| `local/eastasia-preview/logs/` | 5190宿主日志 |
| `local/eastasia-preview/archive/` | 旧预览宿主留档 |
| `local/eastasia-validation/builtin/` | 东亚内置地图及附加测试征服的独立验证档案 |
| `local/eastasia-validation/overlay/` | 完整地图覆盖包的独立验证档案，与用户5190分开 |
| `reports/mod-management-20261020/` | Mod管理浏览器截图；原验证JSON已移至仓库根`reverse/reports/mod-*-browser-verification-20261020.json` |
| `../build/release-*/web/` | 含EXE、基础资源、网页与启动器的独立Windows发行目录；默认存档位于该包`saves/` |

`local/`和`reports/`是本机工作文件，不随源码Git或发行包打包。地图的作者配置仍在`../maps/eastasia_rewrite/`，通用功能构建与账户不再放在地图目录内。历史报告中的旧路径和PID代表当时验证环境，不应作为当前启动参数。

当前可导入的东亚测试征服为`content/eastasia-test-conquest.ew4mod`，使用0.2.0内置东亚资源，增加征服第二页的“东亚 / 地图检查”。278城市无归属、两支原版测试部队，不是正式历史剧本。完整说明见`content/README.md`。
