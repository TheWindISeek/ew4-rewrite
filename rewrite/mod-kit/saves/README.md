# 存档与账户

工具和游戏的 save-root 可以不同。原版继续使用原有目录；Mod使用 `mods/<稳定标识>/<逻辑指纹>/` 保存账户、自动档和手动档。文件中的内部版本和规则指纹仍会被校验，不能通过改文件名强行加载不匹配的局。

仅图片变化且标识、逻辑内容一致时沿用同一组存档；变更规则、将领数据或剧本时产生新的逻辑指纹，旧目录保留。备份请复制完整账户与战局目录，退出游戏后操作，避免把旧快照覆盖新进度。

Android 使用应用私有存储，系统文件选择器导入的源文件仍留在用户选择的位置。卸载/清除应用数据会删除应用私有内容，请事先另行备份。

## 实际路径与文件

| 运行方式 | 存档根目录 | Mod安装根目录 |
| --- | --- | --- |
| 独立Web发布默认 | 程序目录/saves | saves/content-mods |
| CLI工具默认 | 工具目录/saves | 工具目录/mods |
| Android | FilesDir/saves | FilesDir/content-mods，系统私有目录，不是下载目录 |
| 开发预览 | 由启动参数--save-root指定 | 默认在save-root下content-mods，可用--mod-root覆盖 |

当前开发5184使用`rewrite/reports/frontend_restoration_20261019/test-profile`，不是CLI工具的saves。路径以启动参数为准，不应依赖某个历史进程号或历史战局快照。

原版根目录有account.json；新模式槽为`saves/mode-1/slot-0.json`（战役）、mode-2（征服）、mode-4（教程），槽0～6。session.json及旧slot文件为兼容路径，不一定是最新模式槽。Mod对应`mods/<id>/<logicRevision>/`下再出现这些账户和槽文件。

`ModStore.NewGame()/LogicRevision()`决定隔离位置；`ClientSaves.SavePath()/ReadSlot()/AutoSave()`决定槽路径；`ClientGame.Load()`检查内容版本。不要把baseRevision、安装key和logicRevision当同一个值。当前原XML/INI也参与逻辑指纹，哪怕只改文字，也可能产生新逻辑目录；图片/frontend配置不在该计算范围内。

只复制account.json不等于备份战局；仅复制session.json也不保证保存了最新槽。备份完整档案目录。当前没有UI导出存档按钮，CLI export是导出Mod资源，不含存档。
