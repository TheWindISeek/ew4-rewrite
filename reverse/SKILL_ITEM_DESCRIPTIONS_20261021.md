# Rewrite 将领技能与物品描述核验

日期：2026-10-21。对象：核验时工作树源码与内置原版资源。

范围澄清：本报告只核验说明文本和界面显示，不能证明说明中的作用实际生效。用户澄清后的实际规则核验见 [SKILL_ITEM_EFFECTS_20261021.md](SKILL_ITEM_EFFECTS_20261021.md)。后续已修复经验装备类型及32种装备数值，见 [修复记录](EQUIPMENT_EFFECT_FIX_20261021.md)；箱子入口及六种消耗品实际使用也已接通，见 [消耗品交付记录](CONSUMABLE_ITEMS_20261021.md)。

## 结论

已实现原版将领技能说明，以及装备页和商店中的物品效果说明。未发现原版描述条目缺失或技能编号错位。

- 588 条将领定义的 1446 个技能绑定，覆盖全部 40 种原技能；客户端返回的说明均与原版 `desc_skill_01` 至 `desc_skill_40` 一致。
- 228 条物品记录，共 54 种名称，全部具备中文说明，无空文本、未解析键或与原版不一致的描述。
- `rewrite/data/original/def_item.xml`、`stringtable_cn.ini` 分别与 `apk_work/original/assets/` 同名文件 SHA256 完全一致。

## 显示入口与实现依据

| 入口 | 实际行为 | 源码 |
| --- | --- | --- |
| 将领详情 | 技能旁信息按钮点击固定侧向提示框；悬停、键盘焦点也可显示，外部点击/Escape关闭 | `rewrite/Ew4.Web/wwwroot/menus.js` 的 `commanderInfo`；`commander-workflows.js` 的 `skillTip` |
| 技能数据 | 原技能内部零基编号转换成一基文本键，输出名称与描述 | `rewrite/Ew4.Client/ClientExtensions.cs` 的 `SkillView`；`ClientHeadquarters.cs` 的 `Headquarters` |
| 装备界面 | 选择库存物品或已装备物品，更新名称及效果说明 | `rewrite/Ew4.Web/wwwroot/commander-workflows.js` 的 `equipment` / `choose` |
| 指挥部/地图物品商店 | 共用 `shop`，选择商品或出售物品时更新效果说明 | `rewrite/Ew4.Web/wwwroot/commander-workflows.js` 的 `shop` / `choose` |
| 物品数据 | 按物品定义名称读取 `desc_` 文本，不根据数值重新生成 | `rewrite/Ew4.Client/ClientCommerce.cs` 的 `ItemViews` |
| 战场部队信息 | 部队技能列表可打开单独技能说明 | `rewrite/Ew4.Web/wwwroot/app.js` 的 `openUnitInfo` / `openSkillDescription` |

## 本轮验证

隔离 5213 端口、独立编译输出及测试存档运行当前源码，未使用现有玩家服务。测试后已停止本轮服务。

- 10 项浏览器检查通过，异常 0、失败资源请求 0。
- 实际遍历 40 种技能的侧向提示框，文字均与绑定描述一致；点击及 Escape 关闭通过。
- 遍历 54 种物品的装备页选择与说明渲染，全部一致。该项使用浏览器内存中的展示库存，不提交装备或购买事务。
- 指挥部商店实际 7 个商品位逐个选择，说明全部一致；商店库存初始化使用本轮隔离账户。
- 保存 5 张桌面/手机横竖屏截图，已目视检查技能竖屏、装备桌面及商店横屏截图，中文与资源正常显示。
- 核验脚本通过 `node --check`；当前 Web 源码成功编译并启动。

证据：`rewrite/reports/description-audit-20261021/verification.json`、同目录 `verify.cjs` 与 PNG 截图。

## 边界

- 将领详情的装备槽目前只有图片，没有独立点击/悬停说明；需进入“装备”界面选中查看。这不影响装备页已有说明，但并非所有装备图标都可直接查看效果。
- 文案沿用原版，部分只概述作用。例如黄金权杖说明“可以提高爵位的晋升速度”，没有自动展开定义中的具体数值。这次确认的是描述数据及显示，不是重新审计全部技能/装备数值机制。
- Mod 可覆盖文本表；只改 `function/value/target` 不会自动重写说明，作者需同步维护对应文本。
- 本轮没有操作真实对局、更新 APK、核对每份历史发行包或进行 Android 真机验收。地图商店复用同一显示函数，本轮实际商品点击验证在指挥部完成；战场技能入口以源码核对为依据。
