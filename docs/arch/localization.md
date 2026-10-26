# 多语言（Localization）

状态：**已实现（首期）**。字体不在本文范围（沿用现有字体包，见 `mods/base/assets/client/fonts/README.md`）。

## 1. 目标与边界

- 支持三种语言：`zh-Hans`（简体，默认）、`zh-Hant`（繁体）、`en`（英文）。
- 语言是**内容包的数据**，不是代码：mod 想加翻译，只需放文件，不改引擎。
- 缺翻译就回退，不报错：游戏永远能显示出东西。
- **不碰原版 APK 的语言**：导出 APK 时多语言文件属于"原版不认识、自动忽略"的内容。
- 地名（`cityLanguage` / `geographicLanguage`）是数据里的双语字段，保持独立设置，不并入本机制。

## 2. 数据

```text
assets/client/lang/<code>.json
{ "schema": 1, "strings": { "<key>": "<text>", ... } }
```

- `<code>` 取 `zh-Hans` / `zh-Hant` / `en`。文件可以只含部分 key。
- 原版字符串表 `original/stringtable_cn.ini`（约 1400 条，key 如 `name_Napoleon`）是**最底层默认**，仍是简体。
- 界面自己的文字使用 `ui.` 前缀的 key（如 `ui.save`、`ui.settings`）。

### 回退链

```text
当前语言覆盖表 → zh-Hans 覆盖表 → 原版字符串表 → key 本身
```

例：英文下 `ui.save` 来自 `en.json`；`name_Napoleon` 如果 `en.json` 没有，就显示原版简体"拿破仑"。

## 3. 后端

| 组件 | 位置 | 职责 |
|------|------|------|
| `Languages` | `Ew4.Service/Localization` | 受支持语言列表、规范化（未知 → 默认） |
| `LocalizedStrings` | 同上 | 从内容包读 `client/lang/*.json`，按回退链合并出某语言的完整表 |
| `UserSettings` | 同上 | 玩家本机设置 `<user>/settings.json`（目前只有 `language`），跨 mod 共享 |
| `PlayContent` | `Ew4.Service/Play` | 构造时就绑定一种语言（战役名、章节名、描述都在构造时译好） |
| `play.language` | Plan，route `language` | 读/改语言 |

**切换语言 = 重建 `PlayContent` 并清掉局内会话。** 战役名、描述等是构造时译好的，不做"每次渲染再翻译"，实现最简单。切换是低频操作；局内进度以自动存档为准（前端切换前提示）。

### `play.language`

- `{"op":"get"}` → `{ok, data:{language, available:[{code,name}], strings:{…}}}`
  - `strings` 只含覆盖表合并结果（`ui.*` 和翻译过的游戏文字），不含原版 1400 条（前端已有 `manifest.strings`）。
  - `available` 是语言列表，只列内容包里有覆盖文件的语言（`zh-Hans` 始终可用）。
- `{"op":"set","language":"en"}` → 保存、重建、返回与 `get` 相同的数据。未知语言 `language.unknown`。

## 4. 前端

- `i18n.js`：`Ew4I18n.t(key, fallback)`。启动时 `rpc('language')` 取表，叠到 `manifest.strings`；`t` 只是查表。
- HTML 静态文字用 `data-i18n="ui.xxx"`，启动时批量替换。
- 设置里新增"语言"选项；切换后调用 `play.language set` 并 `location.reload()`（canvas 文字需要重绘）。
- 迁移范围（首期）：主菜单、战役/征服选局、存档菜单、设置、暂停菜单、音乐/启动提示、战斗常用文字。编辑器相关页面（`editor*.html/js`、`mods.js`）是作者工具，**暂不迁移**，保持中文。
- 没迁移的写死中文不会坏，只是不随语言变。

## 5. 内容

| 语言 | base 的内容 |
|------|-------------|
| `zh-Hans` | `ui.*` 全部界面文字 |
| `zh-Hant` | `ui.*` + 原版字符串表整表（由简体经 OpenCC `s2twp` 转换并生成，需人工校对） |
| `en` | `ui.*`（人工翻译）；游戏内容（人名、描述等）**暂回退到简体**，等翻译 |

生成繁体的脚本：`scripts/dev/gen-zh-hant.py`（宿主机或 dev 容器内运行，依赖 `opencc-python-reimplemented`）。

## 6. 给 mod 作者

1. 在 `assets/client/lang/` 放对应语言的文件。
2. 只写想覆盖的 key；其它自动回退。
3. 想给自定义剧本名做多语言：把 `name` 写成字符串 key（如 `my_mod.scenario.1`），在各语言文件里给出译文。

## 7. 非目标

- 不做运行时热切换（需要重载页面）。
- 不做复数、性别等 ICU 级别格式化；需要插值的文字用 `{0}` 占位，由前端 `t(key, args)` 替换。
- 不翻译编辑器界面。
