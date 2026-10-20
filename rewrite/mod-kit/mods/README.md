# 已导入内容包

每个64位十六进制目录是独立安装的内容包，含 `mod.json`、覆盖资源及可能的 `import-report.json`。`active.json` 记录当前选择。导入先在临时目录转换，通过哈希、资源依赖及剧本开局校验后安装；失败不改变当前选择。

不要在游戏运行时手动编辑这些目录。修改请回到作者原资源/覆盖包，重新导入。可用 `export` 导出已验证的 `.ew4mod`，在另一台电脑或手机游戏内导入；无需再次准备原 APK。

存档不在这些资源目录里。清理资源时请先切回原版，保留原始Mod包与saves备份。

## 目录和清单字段

| 文件/字段 | 实际含义 |
| --- | --- |
| `<64位key>/mod.json` | 安装清单；key为该清单内容的SHA256，不是作者填写的id |
| `schema` | 当前为1；不同格式版本会拒绝 |
| `id/name/version` | 稳定的作者标识、显示名称、版本；id影响存档目录 |
| `baseRevision` | 与基础catalog和manifest绑定；不能通过改字符串实现跨版本兼容 |
| `scenarios` | 全部可用剧本ID，不带.btl；不是只写新增关卡 |
| `files` | 覆盖路径到SHA256的映射；没有列出的文件仍使用基础资源 |
| `logicRevision` | 原定义/规则/剧本内容指纹；由Seal计算，决定独立存档分区 |
| `warnings` | 转换提示及原生规则限制；应与实际测试报告一起查看 |
| `import-report.json` | 实际读取的输入hash、未执行的SO名称/hash及转换提示 |
| `active.json` | 当前启用的64位key或null（原版），由Activate原子写入 |
| `.staging-*` | 导入中临时目录；成功后提交，失败后清理；不是可编辑作者目录 |

资源读取入口是`ModStore.ReadFrom`：仅命中`mod.json.files`时读取Mod目录，否则回到内置资源。`Validate`核对文件哈希、规则指纹、所有剧本开局和资源引用；剧本错误集中列出，不会只检查第一个剧本。`Activate`验证成功后才替换当前会话。

## 重新导入和导出

```powershell
.\Ew4.ModTool.exe import "D:\mods\example.apk" --id=example --name="示例改版"
.\Ew4.ModTool.exe list
.\Ew4.ModTool.exe verify "D:\ew4-mod-kit\mods\<list中的key>"
.\Ew4.ModTool.exe export <list中的key> "D:\mods\example.ew4mod"
```

导出的是资源包，不含游戏存档。CLI默认使用工具旁的mods；游戏有自己的安装位置。要在手机启用，请在手机游戏内导入导出的文件。目录位置参见[saves说明](../saves/README.md)。

不能靠修改已安装包内的XML后刷新生效：它与清单hash、转换catalog及逻辑指纹可能同时不一致。应在作者输入副本修改，重新导入；针对rewrite的覆盖包则重新pack、import。删除或切换不会自动迁移到新逻辑版本的旧存档。
