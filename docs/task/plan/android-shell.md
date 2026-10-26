# Android 壳（路线 A：内置后端 + WebView）

状态：**首期实现**（`scripts/ew4.sh apk` 产出 `install/apk/ew4.apk`）。
与 [apk-export.md](./apk-export.md) 无关：那是"改原版 APK 的内容"，这里是"新前端的独立 APK"。

## 1. 决定

- **只做 A**：APK 内带 .NET 后端，启动后在手机本机跑，WebView 打开 `http://127.0.0.1:<port>/`。
- 不做 B（纯 WebView 连远端；要联网就直接用浏览器）。
- 不做 C（把后端改成进程内 JNI / NativeAOT）；以后再说。
- 前端代码一份：`Ew4.Front.Web/wwwroot` 原样使用。

## 2. 结构

```text
APK
├─ classes.dex                 Java 壳（MainActivity / ServerProcess）
├─ assets/ew4-runtime.zip      dotnet publish -r linux-bionic-arm64（自包含）
├─ assets/ew4-mods.zip         install/apk/mods（含 base）
└─ AndroidManifest.xml         targetSdk 28（见 §4）
```

首次启动（或版本戳变化）把两个 zip 解到 `filesDir/ew4/`：

```text
filesDir/ew4/app/      后端可执行文件与依赖
filesDir/ew4/home/     EW4_HOME：mods/、user/（存档、设置）
```

升级时只覆盖 `app/` 与 `home/mods/`，**不动 `home/user/`**（存档、玩家设置）。

## 3. 运行

1. Java 选一个空闲端口，生成一次性 token。
2. 启动子进程：`filesDir/ew4/app/Ew4.Backend.Web --home <home> --urls http://127.0.0.1:<port>`，环境变量 `EW4_ACCESS_TOKEN`、`DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1`、`HOME`、`TMPDIR`。
3. 轮询 `http://127.0.0.1:<port>/` 直到就绪（显示启动画面）。
4. WebView 先写入 cookie `ew4_token`，再加载页面。
5. Activity 销毁时终止子进程；回到前台时检测子进程是否还活着，没有就重启。

## 4. 关键技术点

| 问题 | 做法 |
|------|------|
| ASP.NET 没有 bionic 运行时包 | `Microsoft.NETCore.App` 用 `linux-bionic-arm64`；ASP.NET 的托管 dll 取自 `linux-arm64` 包（托管代码与 libc 无关）。见 `src/Ew4.Backend.Web/android.targets` |
| Android 10+ 禁止执行应用数据目录里的文件 | `targetSdkVersion=28`（Termux 同款），此时 `filesDir` 内的文件可 exec。仅用于侧载；不上架 Play |
| 没有 ICU | `InvariantGlobalization=true` |
| 没有 OpenSSL（.NET 的 SHA-256 依赖它） | `Ew4.Interface.Common.Sha256Hash`：系统实现不可用时退回托管实现，结果一致（有测试；`EW4_FORCE_MANAGED_SHA256=1` 可在桌面强制走托管路径验证） |
| 手机上不要服务器 GC | `ServerGarbageCollector=false`、`ConcurrentGarbageCollection=false`；环境变量 `DOTNET_EnableWriteXorExecute=0`、`DOTNET_EnableDiagnostics=0` |
| 本机端口被其它应用访问 | 只绑 `127.0.0.1`；若设置了 `EW4_ACCESS_TOKEN`，后端要求 cookie `ew4_token` 匹配，否则 403 |
| 体积 | 运行时约 70 MB + base mods 约 160 MB；首期不裁剪 |
| 架构 | 只出 `arm64-v8a` |

## 5. 构建

宿主机只需 Docker。内置哪些 mod 由 `EW4_APK_MODS` 决定（空格分隔，默认内置仓库 `mods/` 下的全部 mod）。镜像 `ew4-android:local`（`.config/docker/Dockerfile.android`）= dotnet SDK + JDK 17 + Android 命令行工具（`platforms;android-34`、`build-tools;34.0.0`）。

```bash
scripts/ew4.sh apk            # → install/apk/ew4.apk（debug 密钥签名，可直接侧载）
```

步骤：`dotnet publish -r linux-bionic-arm64` → 打 zip → `aapt2` → `javac` → `d8` → `zipalign` → `apksigner`。无 Gradle。

## 6. 验证与已知限制

- 构建环境里无法运行 bionic 程序，**真机验证由人工完成**。
- 排查：`adb logcat -s Ew4`；壳把后端 stdout/stderr 写进 `filesDir/ew4/home/user/logs/backend.log`。
- 首次启动要解压，几十秒。
- 多语言、BGM、存档与 Web 版一致（同一份后端）。
- 暂不支持：mod 导入 UI 的文件选择（WebView 文件选择器已启用，但未测试）、后台保活。

## 启动自检与环境档位（exit 139 排查）

真机上后端曾以 SIGSEGV（exit 139）退出，且日志为空（崩在托管代码之前或 ASP.NET 初始化早期），用户拿不到 `/data/data` 下的日志。因此：

- 后端新增 `--probe`：分阶段打印（runtime / io / sha256 / socket / aspnet-build / aspnet-start / aspnet-request），崩溃时最后一行即出事阶段，全部通过打印 `probe: done`。
- `ServerProcess.PROFILES` 为候选环境档位（默认 → 关 ReadyToRun → 限 GC 堆 → 组合 …）。首次启动依次自检，选第一个通过的档位并写入 `filesDir/ew4/profile.txt`；后端启动失败会清除记录以便下次重测。
- 全部失败时，屏幕（可长按选择）显示设备信息与每个档位的退出码和输出。

## 运行时：Termux 的 Mono 覆盖官方包

- .NET 在 Android（bionic）上的运行时是 **Mono**（`libcoreclr.so` 是 Mono 的，没有 `libclrjit.so`）。
- 官方 `Microsoft.NETCore.App.Runtime.linux-bionic-arm64` 的 `libcoreclr.so` 在真机（Android 14）初始化时 SIGSEGV；host 跟踪停在 `CoreCLR path = ...`。
- 解决：打包时用 Termux 同版本（10.0.13）的 `dotnet-runtime-10.0` 包覆盖 `Microsoft.NETCore.App` 的 `*.so`/`*.dll`（`scripts/dev/android.sh` 的 `ew4_android_overlay_runtime`，缓存 `build/android/cache`），并删除无用的 Mono 静态库。升级 .NET 小版本时需确认 Termux 有同版本。
- 无 OpenSSL：外壳设 `EW4_FORCE_MANAGED_SHA256=1`，否则 `SHA256.Create` 会让 Mono `abort()`。
- 已在 Android 14 真机验证可启动并进入游戏；性能（首次解压、Mono 解释执行）后续优化。
