# BTL JURI v1：手工辖区交付记录

用户要求：每个剧本BTL可逐格指定辖区中心，有显式记录时不用自动划分；无记录的旧剧本继续自动生成，制作规则与函数入口写进README指南。本轮已实现，不修改内置剧本数据，不更新APK。

## 实现入口

- `rewrite/Ew4.Legacy/LegacyBattleExtensions.cs`：16字节`EW4XBTL\0`容器头、16字节块头，JURI v1必需块；payload为64字节元数据/主体SHA256及int32逐格中心数组。容器限64MiB/64块；重复、未知必需块/版本、截断、额外尾部、无效元数据均失败。未知可选块由工具保留。
- `LegacyResources.cs::LegacyBattle`：按原主体段长度定位扩展，`ToBytes`保留完整文件，`ToLegacyBytes`取原主体，`WithJurisdictions`替换或移除JURI。
- 记录顺序为剧本矩形局部行优先数组，元素为BTL设施使用的全底图中心格编号；-1明确无辖区。城市/工厂/马厩为中心且自身指向自身，水格/港口/无效格不可分配；中心须在剧本区域内。
- `Core/TerritorialJurisdictions.FromAssignments`校验地形/设施并建立固定陆地接壤图；隔海行政标签为detached，不新增跨海染色资格。现有中心占领、友军、农田/要塞和建堡规则继续使用同一固定数组。
- `ClientContent.Start`在战役/征服优先应用BTL数据，无数据才调用Create；教程含手工辖区会拒绝。原主体和无扩展的旧指纹保持原算法；带扩展的源字节参与现有会话/Mod逻辑指纹，保存固定辖区，不静默重绑旧档。
- Mod导入原本就保留BTL字节；扩展通过共享离线读取及逐剧本开局校验，不增加SO/DEX/任意脚本执行入口。
- `ModTool/JurisdictionCommands.cs`新增`jurisdiction-export/apply/strip`，支持`--content`匹配Mod转换资源。JSON严格校验，apply走真实开局后才写新文件，拒绝覆盖；CLI不创建或切换玩家账户。

## 文档与工具

详细作者指南：`rewrite/mod-kit/docs/porting/BTL_JURISDICTIONS.md`，包含完整偏移/类型/字节序、版本及flags、坐标换算、命令、来源校验、旧编辑器、存档与双端生效边界、函数和错误码。根README、移植README、data/original及maps README、配置/源码/入口表/兼容说明已导航并移除“显式辖区尚未实现”的旧表述。78个本地文档链接校验，无失效。

已发布电脑工具到`build/ew4-mod-kit`，1965文件/185445350字节；直接运行发行版exe导出3604格成功。工具包指南哈希与源码文档相同。单JURI增加`96+4*N`字节；conquest1的3604格增加14512字节，不要求分发编辑JSON。

## 验证结果

- 全后端：309595项，含JURI专项228，0错误；报告`rewrite/reports/core_validation.json`及`jurisdiction-extension-20261020/core.json`。内置95份BTL读取/字节保持、全部内置战役/征服自动结果写回、手工非最近中心、实际占领转属、建堡、跨海限制、存档/绑定及协议异常覆盖。
- 客户端原回归：859项，0错误。
- 共享Mod导入：77项，0错误，其中新增11项实际目录/ZIP/ew4mod回导、启用、逻辑隔离、自动存档及错误包不替换当前包。默认报告目录`mod-import-20261019/60a484d3678e49f9a9c2f3799a89ecc4`；新增专项在`jurisdiction-extension-20261020/import-*/results.json`。
- `tools/verify_jurisdiction_tool.cjs`：28项，0错误，实际CLI导出/写回/重导出/strip、源文件保护及错误JSON；最终目录`tool-1792467507568`。
- `tools/verify_jurisdiction_web.cjs`：独立5185真实ZIP文件选择/导入、启用、选国开局、核对存档作者中心编号、重载和原版回退，8项/2图，0浏览器异常/资源缺失；`web.json`与截图已保存并目视检查。测试包单独演示岛屿行政归属，陆上占领转属另由核心大陆组件用例验证。
- Android `-t:Compile`通过，显式使用本机`.codex/tools/android-sdk`及`android-jdk`；没有构建/更新APK或设备运行验收。

## 预览与保护

5184已切到`rewrite/local/jurisdiction-runtime`，PID35616。更新前即时读取为主菜单、无运行会话，保持原活动内容；1741个档案文件哈希全部一致，没有加载历史战局、写入存档或切换玩家Mod。证据`rewrite/reports/mod-import-20261019/preview-preservation.json`。独立5185/PID32196已停止，隔离账户为`rewrite/local/jurisdiction-preview-profile`。

后续作者可使用新增CLI制作并通过当前Web导入测试；旧Android安装包仍需在用户要求更新时才发布包含新引擎的版本。可视化地图编辑器和全新布兵/事件容器不包含在本次范围。
