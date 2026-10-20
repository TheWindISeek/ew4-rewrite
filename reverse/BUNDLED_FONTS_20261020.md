# 内置字库与0.2.4发布

用户要求游戏不用系统字体，内置完整字库，并在本轮完成后提交Git/GitHub和上传Web/APK Release。此前Android本地验收暂停上传的记录属于旧阶段，本轮明确发布指令已更新范围。

## 实现

- `rewrite/Ew4.Web/wwwroot/original-ui.css`：六个本地WOFF2、统一CSS字体链、禁用合成粗体；移除所有Georgia/系统字体声明。
- `original-ui.js::prepareFonts/TextRenderer.prepare`：覆盖索引及六字库全部加载后才允许测量与Canvas绘制。
- `hasGlyph/displayText`：按码点二分覆盖区间、NFC规范化；Canvas缺字显示内置U+FFFD并提示码位。保留城市白填充/黑描边。
- `battlefield.js`：中文地理标注同样使用规范化后的码点与内置字体。
- `MainActivity.LocalClient.ShouldInterceptRequest`：增加WOFF2/TTF/OTF MIME。Android原有启动、全屏、安全区、返回键和原图标改动保留。
- Noto Serif SC Regular 400主字体；Jigmo 20250912三平面补字；GNU Unifont/Upper 17.0.05补充其他文字。六份WOFF2共30,512,720字节，cmap并集192,816码位，不作子集裁剪。旧25,928,940字节WenKai及旧许可归档到`rewrite/local/font-sources-20261020`，不再发行。
- UTF-8是编码，覆盖按`coverage.json`查询，不保证未分配、私用区和未来Unicode。真实cmap覆盖不是所有字形视觉质量或复杂文字排版的保证。

## 构建与证据

- `tools/build_bundled_fonts.py`：Noto实例化400字重、完整WOFF2压缩、转换前后cmap校验、精确SHA-256/来源/覆盖索引及runtime-content字体项；本地源文件不重复上传。
- `tools/audit_bundled_fonts.py`：实际发行字库哈希/cmap与原版、本地六个APK XML/INI文字核对；NFC规范化，控制/私用字符排除。报告`rewrite/reports/bundled-fonts-20261020/coverage-audit.json`无缺字。
- `tools/verify_font_regression.cjs`：源码及独立发行包各42项、10图，4个桌面/手机视口；六字库本地请求，CDP实际字形为自定义字体，扩展平面汉字有独立非空像素，城市填充/描边分离、商店居中与国家长名边界通过；异常/缺失均0。报告同目录`browser/`与`published-browser/`。
- 客户端859、Mod77检查通过。未重复声称全套旧核心回归或所有原生Mod机制都已兼容。
- `tools/verify_web_distribution.py`：独立Web3670检查，全部前端/运行资源/字体/索引哈希/启动入口/依赖通过，包内无存档；`build/release-0.2.4-20261020/web-verification.json`。
- ARM64 APK：0.2.4/versionCode6、110,410,230字节，SHA256 `d0a2968da233c675360d89e731972d8925234aebf95aeb2a8068651f3a406487`；资源和当前程序集、CRC、v2/v3签名、16KB对齐通过。本轮无Android设备运行，先前0.2.3模拟器验收不替代本版验收。
- Android独立ArtifactsPath首次构建因netstandard项目RID输出路径不一致失败；改用常规obj并显式`-r android-arm64`构建成功，未改变玩法代码。JSON换行归一后重建APK，最终报告检查的是新包。

## 发行与运行边界

发布说明：`rewrite/RELEASE_0.2.4_20261020.md`。本地完整目录`build/release-0.2.4-20261020/web`和`android`，附件在`assets`；`tools/package_release.py`要求干净提交，生成源码HEAD、SHA256SUMS和release-manifest。上传后以`release-remote.json`和`remote-audit.json`核对远端tag/提交/附件大小与哈希。

发布入口：https://github.com/aztksilence111/ew4-rewrite/releases/tag/v0.2.4-20261020 。源码只保留运行字库/代码/文档，Web ZIP和APK上传Release，不把构建副本、Mod APK和私人存档入库。

详细字体许可和字段/函数指南已同步`rewrite/mod-kit`及`build/ew4-mod-kit`。5184保持原进程/账户；5190仅补充共享字体文件以适配源码前端，旧兼容引擎、基础manifest/catalog、Mod与存档保持原状。5191旧版预览仍在，未强制结束；运行新版前应停止旧服务，或在独立端口运行。所有5185字体验证均使用隔离档案。
