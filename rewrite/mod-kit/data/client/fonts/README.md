# 稳定文字与字体

当前rewrite使用本目录的六份内置WOFF2字库，不读取系统字体。常用文字使用Noto Serif SC Regular 400细宋体；Jigmo三份字库补充生僻汉字；GNU Unifont及Upper补充其他文字。文本与美术图片分开处理，导入Mod只读取文字内容，避免白字白边和基线浮动。

## 修改入口

| 目标 | 当前入口 |
| --- | --- |
| 将领名、按钮文字、说明 | 原`stringtable_cn.ini`和相关XML，修改后重导入 |
| 城市原名 | `def_areaname.xml`及剧本地点引用，修改后重导入 |
| 城市中英文对应 | `frontend/city-names.json`，自创名需作者补译名 |
| 字号、颜色、描边、对齐 | `original-ui.js`的`TextRenderer.layout/element`及调用点；暂无完整Mod文字样式配置接口 |
| 标题logo等文字美术 | 配套图集和布局引用，作为图片替换 |

`TextRenderer.prepare`加载离线字体，`layout/element`按字号角色绘制；城市白填充黑描边。历史`manifest.fonts`的`height/glyphs/preserveColor`、FNT/PNG字形及`ui.labels`预渲染字不再决定当前文本样式。版本3导入器不导入这些字库样式并清空旧文字缓存；旧产物保留元数据也不代表仍使用它。

## 控件文字适配（2026-10-20）

源码位置为`rewrite/Ew4.Web/wwwroot/`。调整下面的入口时保留同一组内置字体族，按控件用途分别指定颜色和尺寸：

| 范围 | 文件与函数 | 当前约定 |
| --- | --- | --- |
| 共用绘制 | `original-ui.js::TextRenderer.element(text,font,color,width,align,options)` | `width`是包含留白的逻辑像素总宽；不是CSS变换后的屏幕像素 |
| 单行长名称 | `TextRenderer.layout/element`的第六参数 | `{singleLine:true,size:11.5}`按可用宽度重新绘制较小字号，不强制换行或横向拉扁；`size`可省略以使用字号角色 |
| 限高控件 | 同上 | `{singleLine:true,maxHeight:18}`同时适配总高度；多行说明保持换行，不使用单行选项 |
| 普通HTML文字 | `TextRenderer.forElement/paintTree` | 读取控件CSS的`font-size/color/text-align`，按钮为图标和相邻内容留出空间；不要再统一替换为白色标题字 |
| 入口及国家列表 | `app.js::labelImage/originalLabels` | 主菜单14px浅色，浅底国家条11.5px深色、84px宽；国家列表和“选择国家”标题使用不同路径 |
| 原XML文字控件 | `native-layout.js::Layout.write` | 读取当前Mod布局的`font/color/textcolor/w/h/align/breakwords`；无显式文字对齐的按钮默认居中，保留显式左/右对齐 |
| 战役条目和将领卡 | `menus.js::Menus.lettering`及调用处 | 有限宽度的标题和名称用单行适配；说明正文继续多行显示 |
| 画布缩放 | `original-ui.css`的`.original-text canvas` | 宽高保持比例；不要仅压缩宽度而固定高度 |

例如在源码调用`this.api.text('较长的将领名称','font_text_2_cn_hd','#ebe6e1',74,'center',{singleLine:true})`。此处是开发接口，不是可直接写入Mod清单的新字段。CSS中的字号使用568×320逻辑坐标，外层统一缩放到窗口；不要拿截图中的物理像素直接写入字号。

前端源码修改后刷新Web预览即可；旧APK不会自动更新。可在独立5185测试服务运行`tools/verify_font_regression.cjs`，覆盖字体加载、国家条目边界、商店文字居中和城市填充/描边；另用`tools/verify_sidebar_restoration.cjs`验证战役侧栏。不要把自动化指向玩家正在使用的5184。

不要改`preserveColor`或重画FNT/PNG来修复当前描边。表现包字体角色配置尚在规划；替换内置WOFF2需同时更新覆盖索引、缓存版本和许可，不是已经验证的任意字体切换功能。

## 验证与许可

重导入并启用后，检查中文/英文城市名、长将领名、商店、技能说明和资源数字，核对字形、换行、点击区、填充和描边。原版与Mod往返切换检查内容来源。分步操作见[移植指南](../../../docs/porting/README.md)。

## 字库文件与加载顺序

| 文件 | 前端字体族 | 用途与来源 |
| --- | --- | --- |
| `NotoSerifSC-Regular.woff2` | `EW4 Serif` | 常用中文、西文，400字重；https://github.com/google/fonts/tree/main/ofl/notoserifsc |
| `Jigmo.woff2` | `EW4 Han` | 基本平面汉字；Jigmo 20250912 |
| `Jigmo2.woff2` | `EW4 Han Plane 2` | 第二平面汉字，如𠮷；https://kamichikoichi.github.io/jigmo/ |
| `Jigmo3.woff2` | `EW4 Han Plane 3` | 第三平面扩展汉字，如𰀀；同上 |
| `Unifont.woff2` | `EW4 Unicode` | 其他基本平面文字；GNU Unifont 17.0.05 |
| `UnifontUpper.woff2` | `EW4 Unicode Supplement` | 其他补充平面文字；https://unifoundry.com/unifont/ |
| `coverage.json` | 不适用 | 六字库完整cmap的并集、文件哈希和来源；必须与字体一起发布 |

`original-ui.css`定义六个`@font-face`与`--ew4-font-family`；`original-ui.js::prepareFonts`加载全部字库后才允许`TextRenderer.prepare`完成。DOM与Canvas使用相同顺序，不使用Arial/宋体等系统字体别名，也不合成粗体。Android `MainActivity.LocalClient.ShouldInterceptRequest`以`font/woff2`提供同一文件。

UTF-8只是编码。实际覆盖以`coverage.json::ranges`为准；尚未分配的码位、私用区和任意未来新增Unicode字不保证覆盖。`displayText`按码点处理补充平面汉字，先做NFC规范化；缺字显示内置替代字符U+FFFD并在控制台记录码位，不悄悄切换系统字形。不要把UTF-16代理对切成两个字符。

## 重建与缺字排查

1. 在仓库`rewrite/local/font-sources-20261020`准备Noto Serif SC变量TTF、Jigmo-20250912.zip及Unifont 17.0.05两份OTF。具体文件名与下载地址见`tools/build_bundled_fonts.py::main/recipes`。原始字体仅用于构建，不随发行包重复分发。
2. 使用含FontTools和Brotli的Python运行`python tools/build_bundled_fonts.py`，无字符裁剪地压缩成WOFF2。工具核对转换前后cmap，生成覆盖索引，并同步`data/runtime-content.json`字体项。
3. 运行`python tools/audit_bundled_fonts.py`核对字体哈希、原版和本地APK中的XML/INI字符；结果在`rewrite/reports/bundled-fonts-20261020/coverage-audit.json`。
4. 发现新Mod缺字时先记录控制台的`U+...`，检查来源是否为私用字符/损坏编码；再决定补字库。增加字库需同步`original-ui.css`和`original-ui.js::families/prepareFonts`；覆盖索引中的顺序必须一致。最后重跑审计及独立浏览器验证。

许可：Noto使用`NotoSerifSC-OFL.txt`（SIL OFL 1.1）；Jigmo使用`Jigmo-LICENSE.txt`（CC0 1.0）；GNU Unifont编译字体采用双许可，本发行选择SIL OFL 1.1，完整说明见`Unifont-LICENSE.txt`。保留所有许可。字库在Web与APK中均完整离线内置，不依赖联网下载。
