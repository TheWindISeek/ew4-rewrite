# Mod管理与东亚预览目录整理

2026-10-20，按用户要求将通用功能构建与运行档案从地图作者目录移出。正式代码本已位于rewrite项目；未修改Mod删除语义或游戏规则。

| 原路径（maps/eastasia_rewrite/runtime下） | 当前路径（rewrite/local下） |
| --- | --- |
| mod-delete-build、mod-delete-compat、mod-delete-compat-test | mod-management/同名目录 |
| mod-identity-build、mod-identity-test、web-mod-delete、web-mod-identity | mod-management/同名目录 |
| mod-*、pre-mod-*日志/快照/测试ZIP | mod-management/reports/ |
| web-mod-delete-compat | eastasia-preview/server/ |
| preview-data-before-builtin | eastasia-preview/data/ |
| mods、saves | eastasia-preview/同名目录 |
| web | eastasia-preview/archive/web/ |
| 其他预览日志 | eastasia-preview/logs/ |

实际Move-Item前验证全部绝对路径在仓库范围内、目标不存在；未删除文件。运行中的5190改用新路径，保留同一兼容引擎和基础资源快照；5184与5191未动。重启最初载入旧mode2自动档revision0，随即通过原session.json恢复最新revision4；旧快照8项状态逐项一致，40个原存档与既存哈希记录一致，0文件变化。当前5190 PID33196。证据`rewrite/reports/runtime-relocation-20261020/result.json`。

通用浏览器识别/删除报告从地图目录移至`reverse/reports/mod-identity-browser-verification-20261020.json`与`mod-deletion-browser-verification-20261020.json`；4张截图移至`rewrite/reports/mod-management-20261020/`，报告新增artifactDirectory。历史PID/路径保持原验证记录含义。

`tools/EastAsiaMapVerify`不再使用地图目录runtime：隔离工作目录为`rewrite/local/eastasia-validation/builtin|overlay`。--builtin模式新增征服页覆盖，将测试包导出到`rewrite/content/eastasia-test-conquest.ew4mod`，仅含4个逻辑/菜单文件，复用现有东亚地图。原全覆盖地图包绑定旧基础指纹，继续留档，不作为当前导入入口。

验证器7809项0错误；使用现有0.2.0发行宿主、独立15570和新存档实际验证上传、导入、启用、征服第二页、96剧本登记、conquest7开局（21488格/278城/2部队）、存读档、删除后回原版与3个存档文件保留，均通过，测试服务已停止。证据`rewrite/reports/runtime-relocation-20261020/eastasia-import.json`。

新测试包7522字节，SHA256 `eb6d997119594cfa7069ed1ddf530b75093e93fdb8263996b5dd3ac5032100a4`。包仅供地图检查，不是历史剧本。目录用途及导入步骤见`rewrite/RUNTIME_DIRECTORIES.md`与`rewrite/content/README.md`。无APK更新、GitHub上传或新正式剧本。
