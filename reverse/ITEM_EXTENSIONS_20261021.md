# 物品与装备外置效果接口

用户要求参考将领/兵种技能接口，便利 Mod 作者修改物品/装备并引入外源代码制作新效果。已实现 API 2 的 `itemBindings`，复用现有技能定义、事件总线与包内离线 Lua。

## 交付

- 作者说明：`rewrite/mod-kit/docs/porting/ITEM_EFFECTS.md`。
- 可编辑模板：`rewrite/mod-kit/examples/item-effects/`，5份运行文件，含护佑、主动补给及带持续状态的 Lua 药剂。
- 已验证离线包：`build/item-extensions-20261021/item-effects.ew4mod`，2729字节，SHA256 `eafe0cc1de4298b234afd1229f55af833bf76eb87fa579fd07c15662e99750e2`。
- 独立 Web 预览：`http://127.0.0.1:5261`，启动时PID52060；程序集、测试Mod和测试档在同名build目录，前端从源码读取。其他玩家服务及档案未操作。

## 行为与边界

`itemBindings` 数组按 `ew4.item.N` 定位现有物品，校验ID、重复项、实际存在性、技能引用、物品类型、实例格式和类别。可覆盖value/targetCategory/requiresFlagSkill，或用replaceLegacyEffect关闭原效果。覆盖发生在原版装备数值修正之后，避免作者设置被默认修正覆盖。

非消耗装备通过skills授予主动/被动技能，任职时合入Combatant.SkillSlots，进入现有战斗/回合事件及前端主动栏；卸任时移除，当前战局的换装限制保留。运行目录、名册、商店使用同一份配置后的Equipment，覆盖实际初始将领、购买/装备及名册派遣路径。

消耗品通过useSkill调用现有skill.activate效果，不把技能挂到可反复点击的主动栏。限定active/self、零资源费用、cooldown0、endsAction:false；支持范围效果，沿用原物品对已行动部队可用的规则。保留原效果时先执行原效果并沿用其资格；替换时只执行扩展。一次提交只扣一件库存，各推进一次账户/战局/会话修订。直接致死共用将领退场和死亡通知；施用者死亡停止后续效果，异常不提交早先效果或RNG。

完整实例前缀为item.N.，不同装备状态和冷却隔离。Lua新增ctx.itemId/skillId/instanceId，继续使用有限操作、确定性随机数及状态字典。普通事件状态、技能实例、冷却和持续状态沿用现有保存结构。外源代码通过离线Lua表达，SO/DEX/DLL不作为插件执行。新增物品本体、文字/图片与上架/库存仍走原资源数据；本接口不自动赠送物品，也不自动改旧物品描述。现有AI不会主动施放自定义主动技能。

## 验证

| 检查 | 结果 |
| --- | --- |
| ItemExtensionChecks | 53项，0错误；实际装备/派遣、原效果替换、两个物品同名实例的状态隔离、Lua等级/身份、主动冷却、药剂扣库/状态/重载、无将领使用、直接致死、先治疗后异常回滚及错误配置拒绝 |
| 完整核心回归 | 310893项，0错误；该次运行包含当时48项物品检查，后补5项另经专项运行通过 |
| 完整客户端回归 | 2364项，0错误 |
| ItemExtensionModChecks | 14项，0错误；pack/validate/import/启用、真实客户端用药与存读档、导出后独立再导入、Lua变更逻辑版本隔离、回原版与无效目标拒绝 |
| 浏览器 | 32项，桌面1280x720、手机844x390/390x844；真实导入、装备主动按钮治疗/冷却、原物品窗用药、扣一件、状态及保存重载；0异常/0缺失资源，3截图已检查 |
| 构建 | Web与Android ARM64 Compile均0警告/0错误；JS语法与git diff --check通过 |

系统默认dotnet没有Android workload，改用既有 `.codex/tools/dotnet-android/dotnet.exe`，显式指定现有android-sdk/android-jdk完成Compile；未安装或修改工具链。未打新APK、发布或做真机验收。

原版无扩展基线通过此次完整回归前后，三项内容指纹保持一致：

```text
session dfef71889afdf29187882e4cef1f8af397211c0f27cc68eb1c8d251a53bfeb47
world   db18a6fadc5e7155414d70621f885fb7c42d751e4e5ec24206ec8a073af5ab04
account e47fb936518323a29848404514de602e25bfc04bbc5fafa191485fa32c7d39e5
```

证据：`rewrite/reports/item-extensions-20261021/{backend.json,import.json,web.json,*-items.png}`；重跑命令为Verify/ModChecks的`--item-extensions-only`及`node tools/verify_item_extensions.cjs http://127.0.0.1:5261`。导入测试初次遇Windows临时目录移动占用，重跑通过；后续导出使用独立运行路径，避免重复运行撞已有文件。浏览器重置测试档时需重新Activate清理服务端账户缓存，保持账户较新版本优先的现有行为。

工作树另有国家目录/BTL国别扩展、兵种外观、将领标签与名册管理等并发修改，均保留。本次未提交Git或更新发行版本；旧0.2.9不包含本新入口。
