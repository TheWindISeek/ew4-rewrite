# 指挥部商店刷新

日期：2026-10-21。用户最终范围：先实现商店刷新；仅指挥部商店支持，地图商店保留剧本作者配置的独特商品，不刷新。本地多人模式暂缓，未修改胜负结算。

## 行为

- 指挥部商店标题栏右侧新增原版 `button_refresh.png` 图标，工具提示和可访问名称为“刷新商品”。免费刷新。
- 保留原有五个消耗品位（每格库存一份）和两个不同的未持有永久装备位；候选不足时正常留空。
- 消耗品候选超过一种时，每格避开上次的商品类型；永久装备优先抽取上次未出售的类型，保持唯一装备持有约束。
- 库存写入现有账户 `HeadquartersStock`，关闭/重开、刷新网页、重启宿主后保持。已持有物品、勋章、名册和其他账户奖励不变。
- 刷新校验账户 revision，旧刷新/购买请求被拒绝且不改变库存。请求期间按钮禁用，成功后清除旧商品选择；关闭弹窗后延迟响应不重新打开商店。
- 地图商店不显示按钮；后端对带地图 cell 的 refresh 请求直接返回 `trading.command`。本次没有改地图库存、剧本、事件、AI 或胜负源码。

## 源码

- `rewrite/Ew4.Client/ClientCommerce.cs`：指挥部库存重抽与账户版本检查、地图刷新拒绝。沿用既有账户原子保存，不调用世界提交/胜负结算。
- `rewrite/Ew4.Web/wwwroot/commander-workflows.js`、`original-ui.css`：仅指挥部的刷新图标及弹窗生命周期保护。
- `index.html`：刷新上述 JS/CSS 缓存版本，其他会话改动保留。
- `rewrite/Ew4.Verify/ShopRefreshChecks.cs`、`tools/verify_shop_refresh.cjs`：专项检查，后端并入现有 `--client-only` 入口。

## 验证与预览

- `dotnet build rewrite/Ew4.Web/Ew4.Web.csproj --artifacts-path build/shop-refresh-20261021/artifacts --nologo -v quiet`：零警告、零错误。
- `dotnet run --project rewrite/Ew4.Verify --artifacts-path build/shop-refresh-20261021/artifacts -- . --client-only`：本轮输出 `clientChecks: 1830, errors: 0`。共享 `rewrite/reports/client_host.json` 可能被其他会话覆盖，以本轮命令输出为准。
- JS 语法检查通过；`node tools/verify_shop_refresh.cjs http://127.0.0.1:5211`：18 项通过，浏览器异常/缺图均为零。
- 浏览器报告和四张截图：`rewrite/reports/shop-refresh-20261021/`。已查看桌面及手机竖屏截图，横屏亦通过边界/点击检查。
- 后端覆盖免费/物品与名册保持、唯一装备去重、耗尽装备池、重启恢复、陈旧请求拒绝、地图刷新拒绝、运行中世界/胜负字段保持。浏览器覆盖真实点击、选择清理、关闭重开、网页重载、连点、请求中关闭、手机横竖屏及地图布局无按钮。
- 独立预览：`http://127.0.0.1:5211`，启动 PID 42072；程序集、日志和隔离存档在 `build/shop-refresh-20261021/`。启动时检查端口，未重启其他预览或操作玩家档案。
- 未更新 APK、正式发行包或远端发布；Android 真机未验收。

## 暂缓事项

本地多人尚未实施。已确认规则：新模式使用独立源码组织；只能在进入剧本前通过设定开启，局内不可改；开局默认势力锁定必选；选择其他势力后按原回合顺序轮流操作，未选势力由 AI 操作；仅一个势力完成目标后胜利，其余自动判负，不沿用原单人胜负。
