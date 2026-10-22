# 部队详情及任命将领还原

## 0.2.11 发行续接

用户随后明确授权同步Git并打包Web/APK上传Release。本轮重新构建0.2.11/code13，合并已完成的部队定位/Android安全区修正；未复用其他任务暂停前的中间APK。源码与独立Web的页面52、指挥部94、定位153项检查通过；核心310898、客户端2473及分发2614+4366、独立启动10922项通过。APK前端/程序集、旧证书v2/v3签名、16KiB对齐和版本通过，没有真机执行。

发行说明：`rewrite/RELEASE_0.2.11_20261022.md`。成果与精确源码提交见 `build/release-0.2.11-20261022/assets/release-manifest.json`；上传与远端校验以同目录上层 `release-remote.json`、`remote-audit.json` 为准。独立发行预览为 `http://127.0.0.1:5337`，PID46236，档案在构建目录的 `test-profile/`，不进入发行包。

2026-10-22。原版依据为 `apk_work/original/assets/layout-568h.xml` 的 `form_unitinfo`、`form_generalinfo`、`form_deploygeneral`，以及 `build/tests/frontend_reference_20261018/` 的 `unit_info.png`、`general_info.png`、`headquarters.png`、`headquarters_general.png`。继续使用原包图集及中文字符串；原文 `title_deploygeneral` 为“指挥部”，`btn_deploy` 为“出征”。

## 本轮修改

- 部队详情挂载原版布局，恢复兵种卡、编制标记、真实招募费用、属性、将领头像及蓝色信息按钮、说明和装饰；修正编制空位裁片名称。
- 按用户两次图片批注，顶部三个区域统一高 78 设计像素，左右同宽；属性内层随外框伸展，数字字号19适配行高，右侧头像/徽章不越底边。不能只比较外框，必须检查内部图标和数字的实际边界。
- 原说明区下预留独立“技能 / 效果”区域，总窗高255；长列表滚动，技能说明返回原部队，不依赖全局当前选择。
- 新增只读 `/api/unit-info`，校验 revision/单位存在性，读取实际战局将领。账户按实例ID，剧本/酒馆按原BTL索引匹配，覆盖己方和敌方，保留当前军衔/爵位经验、能力、训练、装备和加成。装备页只查看；信息→装备→信息→部队返回可用。
- 任命场景恢复六列两行、出征人数、分隔线、选择及出征确认，信息按钮单独查看；不受局外筛选影响。局外60槽、筛选、标签和拖动保留。
- 将领信息恢复信息标题、物品原文、整编橙色及原装饰；战局头像不因账户派遣锁变暗，隐藏局外整编/升级入口。

## 验证

- `tools/verify_unit_general_pages.cjs`：52项/14图，异常、缺图、裁切均0。1280×720、844×390、390×844检查三框对齐及图标/数字/人物徽章边界，覆盖无将领、己方、敌方、实际任命、只读/过期请求、装备返回及长标签。
- `tools/verify_headquarters.cjs`：94项/15图，无异常/缺图；`Ew4.Verify --client-only`：2473项、0错误。
- Web构建、Android ARM64 Compile均0警告/错误，JS语法及diff检查通过。Android使用已有 `.codex/tools/dotnet-android`、`android-sdk`、`android-jdk`。

证据：`rewrite/reports/unit-general-20261022/results.json`及同目录PNG。独立预览 `http://127.0.0.1:5278`，PID19812，运行时 `build/unit-general-20261022/runtime-v2`，测试档 `build/unit-general-20261022/saves-v2`。仅替换过自己启动的旧预览PID30452，未操作玩家档案或其他服务。同期其他任务修改了Android、部队定位及文档，保留；发行续接以实际发布记录为准。
