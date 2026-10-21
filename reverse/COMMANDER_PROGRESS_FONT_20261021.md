# 将领经验进度与近似原版字体

## 后续像素修正

按用户截图修正半圆与底圆贴合、上下两行偏差。原贴图含透明边距，弧心在源图 (35,30)，先前 CSS 层放置过高且裁切中心用了参考点；两套底图抗锯齿也略有差别。现以 57x57 原分辨率在单一 Canvas 中合成圆底、按真实弧心裁切的进度条及徽章，并按底图灰色圆轨的像素遮罩限制着色，然后整体缩放，避免多层各自插值。缓存版本 20261021.4。

只读 `tools/verify_rank_gauge_alignment.cjs` 29 项通过：两色各 0/25/50/75/100% 及 3.17/2.25/1.33/0.686 倍缩放，彩色像素全部位于灰色圆轨容差内；桌面、横屏与竖屏共 4 图，异常/缺失 0。报告 `rewrite/reports/rank-gauge-alignment-20261021`。本轮未运行会改动账户的整编测试，未重启用户 5193，刷新页面即加载修正。

## 本轮实现

- `ClientHeadquarters.Headquarters` 新增 `progression.rank/title`，按当前内容的配置表输出每级升级经验，末级为 null；不在前端固定原版级数或经验表。
- `commander-workflows.js::progressionBadge` 共用原版 `borad_rank/borad_standing` 圆底、`bar_rank/bar_standing` 半圆贴图和军衔/爵位徽章。按本级剩余经验比例显示，满级填满；悬停及无障碍属性给出经验数值。原生构造资源依据为 `reverse/native/decompiled/0004dec0.c`。
- 详情、装备、培养前后和整编预览共用该控件；切换供体后刷新实际预览阶段与余经验，整编提交后显示持久化结果。同步修正整编生命加成和恢复值的 XML 占位文字。
- 旧运行后端尚无 `progression` 字段时，只显示已知经验及圆底，不计算未知比例，避免共享源码前端让旧进程报错。完整新显示使用 5193 新后端。

## 字体

用户参考截图的按钮和将领名带楷体笔意。主字库由 Noto Serif SC 改为霞鹜文楷 Regular 1.530，Noto 及现有 Jigmo/Unifont 作为后备。候选不是原版字体的精确复刻。

- 上游：https://github.com/lxgw/LxgwWenKai/releases/tag/v1.530
- TTF 的 SHA-256：`2d73eea94ad808999d90c0000c239a84a33b09d4bc6ab6c5b8bbf1eda8a7fc8b`，与 GitHub Release asset digest 一致。复用已归档的相同源文件，不重复下载。
- SIL OFL 1.1，随运行字库保留 `LXGWWenKai-OFL.txt`；使用完整 WOFF2 转换，不裁剪字形。新增字库 8,098,668 字节。
- 七字库总计 38,611,388 字节、覆盖 192,879 码位。CSS、Canvas 字体链、加载屏障、预加载、覆盖索引及 runtime-content 字体哈希已同步。制作指南已更新。

## 验证及运行

- `dotnet run --project rewrite/Ew4.Verify -c Release -- <root> --client-only`：862 项，0 错误。包含两类动态经验表及末级检查。
- `tools/verify_commander_progress.cjs`：17 项、10 张截图，桌面 1280x720、手机 844x390 和 390x844；验证整编预览/提交/重载、切换供体、培养/装备、0/25/50/75/100% 半圆显示与实际截图不同、满级和旧后端回退，浏览器异常/缺失资源均 0。
- `tools/verify_font_regression.cjs`：42 项、10 张截图，四种视口，七份本地字体、稀有字非空像素、城市描边、文字边界及菜单交互通过。
- `tools/audit_bundled_fonts.py`：原版及六个本地 APK 的 XML/INI 文本缺字 0；所有发行字体哈希与 cmap 索引一致。
- Web Release 编译通过；改动脚本语法检查及 `git diff --check` 通过。未构建新 APK，未做 Android 设备运行。

截图及报告：`rewrite/reports/commander-progress-20261021/`。构建：`rewrite/local/rank-font-runtime`。预览：http://127.0.0.1:5193 ，PID 29704，隔离账户 `rewrite/local/rank-font-profile`。5192 已有其他任务进程，未操作；用户原有存档未读写。
