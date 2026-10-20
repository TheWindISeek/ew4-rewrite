# 基础内容

- `original/`：原资源中的剧本、地形、定义和文字；供后端读取。
- `legacy/`：转换后的规则、兵种、将领目录及原生数值表。
- `client/`：渲染缓存、图集、图片、字库、声音和地图块。
- `frontend/`：征服分页和城市中文名配置。

工具包全部使用这里的资源，无需访问开发工作区或原始解包目录。基础内容用于版本校验，建议保留不动；作者修改在覆盖包中进行。原 APK 资源经导入器转换，不能只把旧 BIN/PKM 放进 client 后就认为已更新。

## 基础路径、覆盖路径与URL

| 基础资源 | 作者覆盖包路径 | 游戏读取位置 |
| --- | --- | --- |
| `data/original/def_commander.xml` | `original/def_commander.xml` | 后端读取，不作为普通图片URL |
| `data/legacy/catalog.json` | `legacy/catalog.json` | 后端规则目录 |
| `data/client/manifest.json` | `client/manifest.json` | `/content/manifest.json` |
| `data/client/ui/main.png` | `client/ui/main.png` | `/content/ui/main.png` |
| `data/frontend/conquest-pages.json` | `frontend/conquest-pages.json` | `/conquest-pages.json` |
| `data/frontend/city-names.json` | `frontend/city-names.json` | `/city-names.json` |

Mod根目录不能再套一层`data/`或作者文件夹。ZIP内应直接看到`mod.json`，或原格式的`assets/`/定义文件。

## 加载与合并方式

原格式由`LegacyModImporter.Convert()`转换；`ModStore.Seal()`写清单，`Validate()`验证，`Activate()`切换，`ReadFrom()`逐文件覆盖内置资源。**没有JSON深层合并**：覆盖manifest或两个frontend JSON时必须放完整文件，不能只放一个修改节点。原资源目录/ZIP缺少的文件可以回退基础资源，但显式提供的同名XML会替换整份XML。

`runtime-content.json`是源码运行数据清单，`import-baseline.json`是转换器变化检测基线，二者不是Mod启用列表。`mod.json`由pack生成，位于作者包根目录，不在本目录。

白名单见`rewrite/Ew4.Mods/ModFiles.cs`的`RuntimePath()`；后端路径映射见`ModBase.Content()`。完整[配置索引](../docs/CONFIG_REFERENCE.md)与[函数索引](../docs/SOURCE_INDEX.md)位于工具包docs。
