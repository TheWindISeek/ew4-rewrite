# 技能与规则扩展开发断点

用户要求先将当前版本备份到 Git/GitHub，再继续开发。本文件记录尚未交付的实现。

## 已写入源码

- Core：SkillSlot 动态数量/顺序/位置；RuleEventBus；WorldSkillActions；ExtensionVictory。
- Legacy：ExtensionLoader；MoonSharp 2.0.0 Lua 解释器；BTL VICT v1 读取/写入；catalog 覆盖。
- Client：加载扩展、共享规则接入、技能状态保存、技能视图/API、主动技能指令。
- Mods：rewrite/ 扩展路径导入/封包、规则指纹、版本化扩展图标路由。
- Web：动态将领技能列表、列表/网格/定位布局、兵种技能详情和主动技能按钮。
- ModTool：victory-export / victory-apply / victory-strip。
- Verify：ExtensionChecks 及 --extension-only；27 项通过。

## 当前验证

- Ew4.Web 与共享库编译通过，0 警告/错误。
- 扩展专项 27 项通过：真实剧本加载、7 技能将领、兵种技能/标签绑定、Lua 伤害修正、
  确定性随机、死循环预算、异常回滚、主动治疗/扣款/冷却、保存恢复、VICT 优先级、
  致死不反击、未知 API/事件/技能拒绝。
- 尚未完成：本轮全部回归、真实离线 ZIP/.ew4mod 导入专项、浏览器显示/点击验收、
  Android 编译验收、完整指南/示例同步与预览交付。
- 27 项测试生成的示例在本地 rewrite/reports/extensions-20261020/example，测试可重新生成。

## 继续检查

- Lua handler 函数存在性目前调用时验证；需要补导入校验及资源/脚本预算边界。
- 检查 Start/End 回合事件、招募事件和技能事务是否覆盖 AI 和玩家共享路径。
- 新技能当前开放伤害修正、反击抑制、治疗和资源奖励；不能声称所有设计事件/效果已完成。
- 新胜负条件要检查 BTL/JSON 空值、非法引用、嵌套深度和起局时满足的处理。
- UI 未做浏览器截图检查，动态数量/锚点及移动端溢出仍需验收。
- 不更新 APK；用户 5184 预览及档案未触碰。

当前任务原始授权：实现开放特性、兵种/将领技能、可变数量和位置、Lua 接口、胜负条件扩展及制作指南。
