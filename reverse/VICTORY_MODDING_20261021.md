# 胜负多阶段扩展与结算界面替换

日期：2026-10-21。范围限于胜负/结算与用户反馈的显示修正；保留并发会话的角色、势力、性能及商店改动。此前结算实现见[原记录](VICTORY_SETTLEMENT_20261021.md)。

## 已交付

- 复用现有事件 `victory_rules` / `victory_default`，可在直接结果、选择按钮和周期事件中多次完整替换/恢复本局规则。没有另建重复阶段状态机。三阶段示例覆盖顺序、待选、存档恢复、最终胜利和同时满足时失败优先。
- `rewrite/extension.json` 新增可选 `victoryPresentation` 路径。按剧本ID配置，`*` 全局回退，无匹配继续原版。配置可内嵌胜利/失败布局、绑定数据/三个命令、指定标题和星图；无需复制整份client manifest。
- `VictoryPresentation.Validate` 校验树规模、尺寸/数值、控件路径/歧义/类型、必需命令、绑定冲突和图集资源。pack/import沿用现有校验链。界面只读后端结算，不控制奖励提交。
- 将领栏按原版78:98竖卡比例，至少六槽、等距排满、有独立间隙；超额横向滚动，保持头像/军衔/爵位，姓名放可访问标签与悬停标题。修复原先inline gap=0覆盖样式导致方块相连且整行偏左。
- 修复原XML门槛控件w=115超出父组w=51：结算数字宽度限制在父容器剩余空间，长数值缩放适配。星级门槛缺省白色，显式Mod节点color/textcolor可覆盖。
- 结算回合/奖励/门槛数字显式选择内置EW4 Serif数字，避开楷体数字的柔和笔形。TextRenderer仅新增可选numeric模式，其他文字默认行为不变；没有使用系统字体或失效的原位图字库。

## 接口与文件

作者入口：[完整指南](../rewrite/mod-kit/docs/porting/VICTORY_MODDING.md)、[示例源码](../rewrite/mod-kit/examples/victory-stages/README.md)。导航与CONFIG_REFERENCE/RULE_EXTENSIONS/SCENARIO_CONTROL已补链接。

实现：Ew4.Legacy/VictoryPresentation.cs、ExtensionLoader的可选配置加载；ClientContent的剧本引用校验；ClientSettlement的结果投影；victory-ui.js/css的布局/绑定/数字/将领；original-ui.js的可选numeric字形；index缓存版本。

`battleResult.presentation` 为可空配置；默认旧宿主响应缺省时仍可显示原版结果。当前宿主需要本轮代码；旧0.2.6包不含此功能。配置格式没有开放HTML/JS执行。

## 验证与产物

| 项目 | 结果 |
| --- | --- |
| 既有结算专项 | 459通过 |
| 新胜利Mod专项 | 32通过；三阶段、待选/中途存档、恢复、胜负优先级、重复刷新不发奖、全局/精确/原版回退及坏配置拒绝 |
| 核心完整回归 | 310030通过，errors=0 |
| 客户端本轮回归 | 1836通过，errors=0；数字仅前端变化，后续没有改变后端规则 |
| Mod封装 | 3份运行JSON，95基础剧本校验，pack/import/activate/export成功，无警告 |
| 浏览器 | 39通过，异常/缺失0；桌面/手机横竖屏、自定义/原版、胜负三按钮、白色衬线数字、999/1000宽度、六槽及超额滚动 |
| Web最终隔离构建 | victory-mod-final，0警告/0错误 |
| Android ARM64 Compile | victory-mod-android，0警告/0错误；无设备运行、新APK或发行更新 |
| 静态检查 | JS语法与git diff --check通过 |

运行证据：`rewrite/reports/victory-mod-20261021/checks.json`、`browser-checks.json`和8张截图；浏览器脚本 `tools/verify_victory_mod.cjs`，专项 `Ew4.Verify/VictoryModChecks.cs` 接入 `--settlement-only`。

可导入示例包：[victory-stages.ew4mod](../rewrite/reports/victory-mod-20261021/victory-stages.ew4mod)。`original-generals.png`及带将领的自定义截图使用浏览器内存五将领展示样本，检验卡片与徽章排版，不代表测试账户实际完成了五将领派遣；战役数字取实际专项结果。

独立预览 http://127.0.0.1:5213/ ，PID35072；构建输出victory-mod-build，账户/Mod均在本轮reports独立目录。此端口此前的描述核验服务已停止，现场确认空闲后才启用本轮服务。网页读取当前wwwroot，启动第一征服可试三阶段示例。未操作玩家服务/账户/存档。运行预览锁定旧输出时，一次原目录重建因文件锁失败，随后改用victory-mod-final构建成功；没有停止其他服务。

获得栏仍显示已记录累计值，本次不新增随机掉落；浏览器手机视口不视为Android真机验收。
