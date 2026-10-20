# 2026-10-20 字体显示修复

用户反馈主菜单和选择国家页面文字异常。本轮仅修改前端文字渲染及文档，不改战斗规则、存档格式或安装包。

## 原因与修复

- `app.js::labelImage`此前将所有标签画成`font_text_4_cn_hd`大号白字，丢失控件本身的字号、前景色和宽度。浅底国家条因此变成白字，长名称超出舞台。现在静态标签读取控件样式，动态国家条使用深色11.5px/84px单行适配；国家标题保持白色17px。
- `original-ui.js::TextRenderer.paintTree`此前仅根据标签类型猜字号，忽略CSS；现在通过`forElement`读取字号、颜色和对齐，并为按钮旁的图标预留空间。
- `TextRenderer.layout/element`的有限宽度现在包含左右留白。新增`singleLine/size/maxHeight/shrinkWidth`内部选项，用缩小字号重画适配长名称；说明正文保留显式换行及自动折行。
- `native-layout.js::Layout.write`按当前布局的宽高适配标题/按钮文字，默认按钮居中，保留显式对齐；`menus.js`将关卡、章节预览和将领卡姓名设为单行。
- `original-ui.css`保持文字画布宽高比，主菜单14px并恢复图案偏移；城市继续使用同一离线TTF，白色填充加独立黑色描边，不使用Mod位图字库样式。
- 更新缓存标识为`20261020.1`，无需重启后端即可读取前端修改。

## 验证

- 修改的4个JS及新增测试脚本通过`node --check`。
- `tools/verify_font_regression.cjs`：31项、10截图；1136×640、1280×720、844×390、390×844；国家条目深色、边界和画布比例、标题样式、设置、商店购买居中、动态Mod管理文字、城市像素及字库元数据隔离。0浏览器异常、0缺失资源。
- `tools/verify_sidebar_restoration.cjs`：69项、12截图，六章背景、条目居中和滑入动画回归通过。
- `tools/verify_mod_menus.cjs fda6cda680ac9b3c120825a6070a62dc9a47fd08f6fdd78ded3cc8129976f062`：18项、20截图，大鏖战真实导入产物/原版切换、主菜单/战役/征服及手机视口、城市中英切换通过。0异常、0缺失资源。此项不是全Mod兼容或完整通关验收。
- 已目视检查主菜单、设置、国家条目桌面/横屏、商店选择、Mod管理及大鏖战/原版往返截图。

机器结果分别位于`rewrite/reports/font-regression-20261020/results.json`、`sidebar_restoration_20261019/results.json`及`menu-compat-20261019/v2/results.json`。后两者为现有回归工具固定输出目录，本轮重跑会更新同名报告。

## 运行与恢复

5184使用`rewrite/local/menu-delivery-runtime/Ew4.Web.dll`，用户预览PID31304，保持运行；前端从`rewrite/Ew4.Web/wwwroot`实时提供。未操作5184游戏、账户、存档或Mod切换。仅独立5185/PID4180用于自动化，账户位于`rewrite/local/font-regression-20261020`；该测试服务收尾停止。

字体README补充实际函数、参数和生效方式，并同步`build/ew4-mod-kit/data/client/fonts/README.md`。未更新APK。后端待发布的其他资源修复不包含在本轮字体结论内。
