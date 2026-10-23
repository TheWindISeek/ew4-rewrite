# 游戏界面程序

此目录保存HTML、JavaScript、CSS，以及征服分页和城市名JSON配置。实际贴图、字库、动画、地图块与声音通过 `/content/` 从当前Mod/基础data读取。

作者修改资源应放在Mod的 `client/`，不要直接改这里的程序脚本。仅 `conquest-pages.json`、`city-names.json` 支持经Mod的 `frontend/` 覆盖。Mod不允许注入JS、CSS或HTML。

Mod入口位于主菜单左下角。此程序与后端及 Android 共用；已发布的移动端安装包需单独更新，电脑源码修改不会自动更新手机端。
