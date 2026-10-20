# 可打包的技能/胜负扩展示例

本目录的 `rewrite/` 是覆盖包内容，可原样复制到作者目录后打包。适配内置conquest1：法国一级重炮增加装甲标签/修理技能；将领`ew4.commander.1`显示七个技能；conquest1改为第20回合获胜、50回合后失败。请用独立测试账户开新局。

```powershell
.\Ew4.ModTool.exe pack "D:\mods\rule-extension" --id=rule-example --name="技能规则示例" --version=1.0
.\Ew4.ModTool.exe verify "D:\mods\rule-extension"
.\Ew4.ModTool.exe import "D:\mods\rule-extension"
.\Ew4.ModTool.exe list
.\Ew4.ModTool.exe export <返回的key> "D:\mods\rule-example.ew4mod"
```

电脑或Android主菜单Mod中导入 `.ew4mod` 后启用。制作原格式ZIP时须包含至少一个所改BTL等原资源文件及这个rewrite/目录；纯扩展覆盖包请使用上述pack流程。

| 目录 | 修改点 |
| --- | --- |
| rewrite/extension.json | 指向定义/脚本/布局，不引用的文件不会生效 |
| rewrite/definitions/ | ID、技能参数、绑定和胜负条件 |
| rewrite/scripts/ | Lua算法；修理次数随战局保存 |
| rewrite/presentation/ | 当前示例将领两列网格、单位单列；可换positioned并修改各槽x/y |

换兵种/将领必须先查自己Mod的catalog ID。只删victoryRules字段即可保留默认胜负，删commanderBindings保留原将领技能。不直接修改内置data或已安装哈希目录。更改JSON/Lua后重新pack/import；逻辑版本隔离可能出现新账户，旧账户仍留在旧目录。

完整字段、Lua上下文、支持事件、BTL VICT和函数位置见[规则接口指南](../../docs/porting/RULE_EXTENSIONS.md)。本例没有自带图标，使用信息图标；可自行放PNG并给技能添加icon字段。
