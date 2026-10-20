# 2026-10-19 Rewrite运行资源迁移

接续 `TERRAIN_HUD_20261019.md` 和 `COMBAT_SYNCHRONIZATION_20261019.md`。用户要求将原包中必要的数据迁入rewrite，避免跨目录依赖影响后续打包。原始备份未移动、未编辑；本轮未更新APK。

## 交付

- `rewrite/data/original/`：按客户端实际读取闭包迁入115个文件，包含95个 `.btl` 剧本、欧洲/美洲地形数据及必要XML/INI定义。不复制原生SO、支付组件、旧图集或已转换的冗余原资源。
- `rewrite/data/legacy/`：原有 `catalog.json` 加上迁入的 `runtime_rule_tables.json`、`metagame_rule_tables.json`。共118项运行数据、2,814,118字节，逐文件SHA-256及大小在 `data/runtime-content.json`。
- `ClientContent` 的逻辑资源名保持 `original/`、`data/`，数据字节不变，源内容指纹及旧存档兼容。迁入不等于新增民国地图/剧本。
- Web通过自身 `data/` 运行，发布时复制必要数据及前端素材；不带 `legacy/animations` 中间研究产物。默认无需仓库路径，存档默认在发布目录 `saves/`；原 `--root` 开发选项仍兼容，新增 `--data-root` 可指定完整数据目录。
- Android资源清单只使用rewrite内部路径，不再通配原包整个assets，也不从reverse报告目录取规则表。评估得到1,745项唯一资源输入，均位于rewrite内，其中原数据115项。
- 使用说明 `rewrite/data/README.md`。`tools/migrate_rewrite_content.py` 为可复核的一次性导入工具，不参与构建；发现目的文件已被编辑则拒绝覆盖。

## 验证

- Release Web独立发布到 `rewrite/local/content-bundle-runtime/`，0编译警告/错误。
- `tools/verify_rewrite_bundle.cjs` 3,863项通过：118项源哈希/发布字节、Android全量输入闭包/逻辑路径、95剧本目录、欧洲/美洲战役/征服/双教程共6新局及存储重载。服务器在发布目录启动且不传 `--root`、`--contentRoot` 或 `--data-root`。
- 从现有预览只读复制的旧存档能在独立服务加载；此验证使用复制时的revision1494旧落盘档，绝未写回5184。客户端859项、机制348项使用新数据路径通过，其中旧内容指纹一致性断言保留。
- 独立发布服务上重新运行资源面板109项/16图，全兵种同步21,976组+488专项/9图，0脚本异常/缺资源。原生显示13,623项0错误。真实海战27项回归见对应报告。
- Android仅执行MSBuild资源项评估，没有构建APK或操作设备。逆向对照测试仍可刻意读取原始材料，这是研究输入，不是运行或打包依赖。

## 当前预览

- 5184已切换 `content-bundle-runtime`，PID37744；仍明确使用原账户 `rewrite/reports/frontend_restoration_20261019/test-profile/`，没有迁移或覆盖用户存档目录。
- `tools/update_presentation_preview.cjs` 在切换前读取当时最新局、保存并备份完整账户，恢复的是当时conquest1 revision1756、274单位。九项状态一致，地块归属变化0；证据 `rewrite/reports/content_bundle_20261019/preview-restart.json`。这只是历史核验，不得覆盖用户后续进度。
- 缓存版本 `20261019.11`，刷新现有页面加载全兵种同步及图标左/数字右的布局。原准备/装填、致死不反击及UI-13用户确认保持。
- 测试仅5185独立账户，收尾停止。下一轮先读本记录，再按任务读前两阶段记录。仅明确要求才更新安装包。
