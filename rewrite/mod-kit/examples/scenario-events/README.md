# 剧本条件、结果和多按钮示例

本例为内置 conquest1 追加中文四按钮开局军议、第 3 回合粮食援助，以及城市数/金币/工业复合条件。可修改 `rewrite/events.json` 的条件、结果和按钮数组。

弹窗主体和下方选项各自支持上下滑动。示例默认仅文字；需要图文组合时，将 PNG 放入 `rewrite/images/council.png`，并在 `popup` 增加 `"image":"rewrite/images/council.png"` 和可选的 `"imageAlt":"军团集结图"`。仅图片时将 `text` 设为空字符串。图片上限 4 MiB，长文本直接写入 `text`。

在作者目录复制本示例的 `rewrite/` 后，使用包含 2026-10-21 事件增补的 ModTool：

```powershell
.\Ew4.ModTool.exe pack "D:\mods\scenario-events" --id=scenario-events --name="剧本事件示例" --version=1.0
.\Ew4.ModTool.exe verify "D:\mods\scenario-events"
.\Ew4.ModTool.exe import "D:\mods\scenario-events"
.\Ew4.ModTool.exe list
.\Ew4.ModTool.exe export <返回的key> "D:\mods\scenario-events.ew4mod"
```

导入游戏并启用，开一个新的第一征服即可看到军议。旧的 0.2.4 安装包尚不含本接口。测试请用独立账户。全字段、死亡/退场语义、阵营转换、指定城市和 Lua 查询见[事件接口](../../docs/porting/SCENARIO_EVENTS.md)。
