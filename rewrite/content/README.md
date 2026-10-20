# 本地测试内容包

## 当前推荐：东亚测试征服

导入文件：**`eastasia-test-conquest.ew4mod`**。

在包含东亚内置底图的rewrite 0.2.0中，主菜单打开“Mod”，选择此文件导入并启用；然后进入“征服”，翻到第二页，选择“东亚 / 地图检查”，选法国开始。场景ID是`conquest7`。

包只附加测试BTL、剧本登记、城市名称覆盖表和征服菜单配置，复用内置东亚底图，所以只有约7.5KB。包含278处无归属城市、272处设施和两支原版占位部队，不是正式历史剧本或势力布局。

重新生成：在仓库根执行`dotnet run --project tools/EastAsiaMapVerify -- . --builtin`。工具写入`rewrite/local/eastasia-validation/builtin/`隔离档案，重新封装本文件；报告记录基础资源指纹、导入和启动检查，不操作5190用户预览。

## 旧完整覆盖包

`eastasia-map.ew4mod`及`eastasia/`是旧的完整地图覆盖包与生成源，曾用于内置整合之前的5190预览。旧包绑定旧基础资源指纹；当前版本优先使用上方测试征服包，不要把旧包或`.previous`当作新版导入入口。仅在重建完整覆盖内容时使用`tools/EastAsiaMapVerify`不带`--builtin`的模式；该模式使用`rewrite/local/eastasia-validation/overlay/`隔离档案。

正式地图资源位于`../data/original/eastasia.bin`、`eastasia.map.json`及`../data/client/maps/eastasia/`。不要直接修改已安装Mod的哈希目录；重新封装导入并启用后再开始新局。测试包依然按Mod标识和规则指纹隔离账户与战局。
