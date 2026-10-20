# 原版显示与交互恢复记录

本轮按共享显示逻辑推进，没有增加民国内容、支付或自定义名册扩展。原材料和日常存档保留；浏览器事务使用 `rewrite/reports/frontend_restoration_20261019/test-profile`。

## 已落盘成果

- 原 XML 窗口组件统一处理高清资源、父子坐标、边框、模板军衔、列表运行时排布、异步动作及重复点击。酒馆原函数 `3eb64` 在 XML 之后按 55 高＋5 间隔重排列表，现按同一规则恢复。装备、晋升、整编可返回原来的公主／将领页面。
- 八位公主高清头像／素描、一次性领取、指挥部原商品槽位、背包买卖、装备／卸下、整编预览／提交、单级及满级培养已接共享账户。新字段兼容现有账户；领取记录、库存、装备和培养结果通过进程重建后的持久化验证。
- 城镇服务采用原 `State6`：1 换资源、2 物品商店、3 酒馆。市场使用原浮点报价规则，交易后读取最新 revision；原初始部队将领的经商属性由客户端适配，不改旧存档内容指纹。
- 战役 faction ID 与旗帜变体分别输出，解决 `gb/gbr`、`de/pru`、`fr/fra` 等原数据不同标识的旗帜绑定问题；重开使用原国家 ID。
- 原方向动画、Motion speed、攻击→装填→收尾、独立开火时轴、命中／反击、AI 移动后攻击及跳过演出接通。原生 `82084` 的 840 组命中特效选择全部匹配。移动尘土／海面尾迹、招募 burst、起火 1/3/2 复合动画已接。
- 特效缓存包含 143 时间轴、41 粒子定义、18 音频；支持 effect/image/animation 三类事件。粒子位移与重力积分仍是近似实现，不能据此宣称逐像素一致；音频是否实际可听仍受浏览器自动播放策略影响。
- 城市独立译名表 `rewrite/Ew4.Web/wwwroot/city-names.json` 覆盖 191 条／190 个原名；中英文切换可保存，英文采用原高清城市字库，国家／海域仍用原地图图层。
- 移动后保持选中及攻击提示；占用格不画可停留点。招募编制切换、显示费用、实际一编民兵结果及扣费已实测；不足资源时禁止确认。

## 验证证据

| 范围 | 结果 | 报告 |
| --- | --- | --- |
| 共享客户端及账户／交易／保存链 | 471 项，0 错误 | `rewrite/reports/client_host.json` |
| 原函数培养／换资源 | 396 比较，0 错误 | `reverse/reports/frontend_service_contracts.json` |
| 原生图层／命中特效／粒子参数 | 13,574 项，0 错误 | `rewrite/reports/frontend_native.json` |
| 相机／路径插值 | 625 项，0 错误 | `rewrite/reports/client_camera.json` |
| 两份教程脚本 | 120 项，0 错误 | `rewrite/reports/tutorial_script.json` |
| 浏览器完整流程 | 17 项，0 异常，0 HTTP 资源缺失，19 张截图 | `rewrite/reports/frontend_restoration_20261019/browser-verification.json` |
| ARM64 APK | 发布成功，v2/v3 签名、16 KiB 对齐、CRC、13 个关键缓存／源码哈希一致 | `rewrite/reports/frontend_restoration_20261019/android-package.json` |

浏览器覆盖桌面 1280×720、移动横屏 844×390、竖屏 390×844；包含非空画布检查、公主→装备／晋升／整编→返回、购物、市场真实扣费及刷新、语言保存、酒馆不重叠、招募、海战、移动保留选择及跳过 AI 后到第 2 回合。截图由同名检查脚本生成，可重复运行，但仅对上述隔离预览使用。

## 交付与边界

- 安装包：`build/ew4-rewrite-frontend-20261019-arm64.apk`，149,669,879 bytes，SHA256 `67808d425ada55f2dffce8a2cddcf359ba52cbc00bf74ae540e5ef27129499b9`。未连接手机／模拟器，未进行 Android 运行验收。
- 预览 `http://127.0.0.1:5184/` 使用隔离测试账户，包含验证中的领取和购买结果，不代表用户日常进度。
- 用户确认仍以 `FRONTEND_ISSUES.md` 为准，UI-13 已确认状态保留。截图检查不等于用户验收全部 38 项。
- 原图集本身有 12 个被引用但无对应叶裁片的节点，见 `frontend_resources.json` 的 `animationGaps`；本轮出现骑兵 `60尾巴2/70尾巴2`，不以相似素材猜补。
- 尚需继续逐场景核准海岸／农田／国界遮挡、相机边缘、特殊攻击变体／战术／完整死亡演出和原像素比例。当前酒馆已显示未来回合条件，但本轮浏览器未等待到可雇佣回合实测雇佣。
- 整编原提示称将领与物品消失，现有共享核心会释放供体装备到背包；原生供体装备处置仍需补证，不能默改已有规则。该差异保留为后续问题。

## 复现入口

`node tools/verify_frontend_restoration.cjs` 验证隔离的 5184 服务。`node tools/test_frontend_native.cjs` 使用原函数向量。`tools/reverse/capture_frontend_services.py` 重新生成原函数合同；`tools/check_android_frontend_package.py` 检查交付包缓存一致性。
