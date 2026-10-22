# 全局国家与长 ID 示例

本目录是可打包覆盖包：原法国通过别名引用 `mod.republic.1792`；使用法国战斗属性、法国旗帜和神罗兵模。征服 1 开局事件复用同一个国家定义创建 `republic.instance.1`，继承玩家模板，其余身份默认来自国家表。该新实例无初始领土或部队，正式剧本应另设领土/部队转交效果。

示例表仅含演示国家，其他原版参战国沿用旧 BTL 默认值。实际项目建议复制完整内置国家表，保留已有定义后新增。新国家键可替换为中文、长名称或其他唯一字符串，不必使用三字母。

```powershell
.\Ew4.ModTool.exe pack "D:\mods\global-countries" --id=global-countries --name="Global countries"
```

完整 ID 写入 BTL 的 `country-export / country-apply / country-strip`、事件引用、顺序和版本要求见[全局国家指南](../../docs/porting/GLOBAL_COUNTRIES.md)。用于 CNTR 时，把对应 ID 写入导出表的 `factions`；保留剧本参战记录顺序，然后 apply 到新文件。国家表仍独立于剧本。
