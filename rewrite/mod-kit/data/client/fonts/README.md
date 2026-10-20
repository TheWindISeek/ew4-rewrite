# 稳定文字与字体

当前rewrite使用本目录`LXGWWenKai-Regular.ttf`（字体族EW4 Extended）绘制文字，不再优先使用原位图字库。文本与美术图片分开处理，避免导入Mod后出现白字白边、混合字体或基线浮动。

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

源码位置为`rewrite/Ew4.Web/wwwroot/`。调整下面的入口时保留同一个TTF字体族，按控件用途分别指定颜色和尺寸：

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

不要改`preserveColor`或重画FNT/PNG来修复当前描边。表现包字体角色配置尚在规划；替换自带TTF需考虑字体族、字符覆盖、尺寸和许可，不是已经验证的任意字体切换功能。

## 验证与许可

重导入并启用后，检查中文/英文城市名、长将领名、商店、技能说明和资源数字，核对字形、换行、点击区、填充和描边。原版与Mod往返切换检查内容来源。分步操作见[移植指南](../../../docs/porting/README.md)。

LXGW WenKai Regular v1.530来源：https://github.com/lxgw/LxgwWenKai 。SIL Open Font License 1.1见本目录`OFL.txt`，替换或再发布时保留许可。
