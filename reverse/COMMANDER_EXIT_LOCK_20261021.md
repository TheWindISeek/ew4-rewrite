# 退出战役后将领卡片残留变暗

日期：2026-10-21。用户反馈 APK 退出当前战役后，指挥部仍将部分将领显示为暗色，并要求修复。

## 原因与修复

- 原 `showMain()` 仅隐藏战场，没有结束共享客户端中的 `game`。`LockedCommanders()` 继续从上一局派遣历史和战场名册计算锁定；卡片因 `data-locked=true` 降到 65% 亮度，同时禁止晋升、装备等账户操作。
- 增加共享 `/api/leave`：在客户端命令锁内校验当前版本，释放内存中的战局、国家、教程断点及移动撤回记录。没有活动战局时允许重复退出；过期退出请求拒绝，不影响当前局。
- 返回主菜单统一先等待后端退出，再清理前端战局、选择、教程、结果窗口与动画，关闭暂停和操作面板。请求失败时保留战场并提示，允许重试。
- 自动档、手动档及账户文件不由退出操作写入或删除。读档沿用现有部署历史，重新恢复本局派遣限制；没有删除或放松战场中的将领锁定。
- Web 与 APK 共用以上代码；`app.js` 缓存版本为 `20261021.24`。没有修改将领贴图、卡片暗色样式或存档格式。

## 验证

- `dotnet run --project rewrite/Ew4.Verify -- . --client-only`：本轮 stdout 为 2251 项、0 错误。新增覆盖过期退出拒绝、退出解锁、无会话动作拒绝、重复退出、所有账户/存档文件逐字节不变、退出后晋升、自动/手动读档恢复部署与锁定。
- `tools/verify_commander_exit.cjs`：26 项、0 异常、0 缺图。实际暂停菜单退出、桌面/手机横竖屏卡片亮度及晋升入口、自动/手动读档再次退出、网络失败保留战局并重试通过。
- 4 张截图在 `rewrite/reports/commander-exit-20261021/`；横竖屏结果已目视核对。浏览器使用独立测试账户，不操作玩家档案。
- Web Release 编译 0 警告、0 错误；JS 语法和 `git diff --check` 通过。Android ARM64 Release publish 成功。

## 本地交付

- 独立预览：<http://127.0.0.1:5224>，PID 48216。运行时及独立档案位于 `build/commander-exit-20261021/`，前端为源码目录。
- APK：`build/commander-exit-20261021/ew4-rewrite-0.2.7-20261021-arm64.apk`。本次本地构建通过命令参数指定版本 0.2.7 / code 9，项目默认版本和已有发行附件未改。
- 构建命令使用专用 `C:/Users/WIN11/.codex/tools/dotnet-android/dotnet.exe publish`，显式指定 `-r android-arm64 -p:RuntimeIdentifiers=android-arm64`、本机 Android SDK/JDK、`-p:ApplicationVersion=9 -p:ApplicationDisplayVersion=0.2.7`，输出到本目录的 `android/`。
- 最终文件 125403057 字节，SHA-256：`075812b36fde8811f61e11ffd8d41579b395b2d0a30f644b3b5e1aff396e5d22`。CRC、全部运行资源、共享前端和 ARM64 程序集 blob 字节核验通过；仅 `arm64-v8a`，v2/v3 签名及 16 KiB 对齐通过。
- 签名证书 SHA-256：`82e8ba69b415d50193f7a6c2fd95d9d67f39d396bc321c1f0b03cf4bfb7c9df1`，与既有 0.2.4/0.2.5/0.2.6 相同。包内最终校验结果见同目录 `android-verification.json`。
- 构建时保留共享工作树其他任务的修改。一次核对遇到并行任务更新资源清单，重新打包后逐资源核对通过；未回退这些更新。未安装到现有设备、未做真机运行、未发布 GitHub Release；其他任务和玩家服务未重启。
