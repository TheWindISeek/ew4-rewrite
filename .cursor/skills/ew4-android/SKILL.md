---
name: ew4-android
description: >-
  Build, diagnose and ship the EW4 Android APK (Java WebView shell + embedded Mono backend). Use when building the APK, changing src/Ew4.Android, android.targets, the runtime overlay, or debugging on-device startup crashes.
---

# EW4 Android（路线 A）

约束见 [docs/arch/frozen/platform-shells.md](../../../docs/arch/frozen/platform-shells.md)（冻结，不改）。设计与已知限制：[docs/task/plan/android-shell.md](../../../docs/task/plan/android-shell.md)。

## 构建

```bash
scripts/ew4.sh apk                      # 产出 install/apk/ew4.apk（debug 签名，可侧载）
EW4_APK_MODS="base ww1" scripts/ew4.sh apk   # 仅内置部分 mod；默认内置 mods/ 全部
```

流程：同步 mods → `dotnet publish -r linux-bionic-arm64 -p:Ew4Android=true` → **用 Termux 的 Mono 10.0.13 覆盖运行时**（`ew4_android_overlay_runtime`，缓存在 `build/android/cache`）→ `ew4-android:local` 容器内 `src/Ew4.Android/build.sh`（aapt2 / javac / d8 / zipalign / apksigner）。需要 Docker，涉及 docker 的命令在沙箱里要放开权限。

## 不要做的事

- 不要换回官方 `linux-bionic` 运行时包：真机在 libcoreclr 初始化时 SIGSEGV。
- 不要用系统 `SHA256`/OpenSSL：Mono 会直接 abort；外壳已设 `EW4_FORCE_MANAGED_SHA256=1`。
- 不要把 `targetSdkVersion` 提到 28 以上（应用数据目录 exec 会被禁止）。
- 不要删启动自检（`--probe`、`ServerProcess.PROFILES`）与屏幕诊断；用户拿不到设备日志。
- 不要分叉前端。

## 真机排障

用户看不到设备日志，靠屏幕诊断：

1. 首次启动依次用 `PROFILES` 跑 `--probe`，选第一个全通过的并写 `filesDir/ew4/profile.txt`。
2. 全部失败时屏幕显示每套的退出码与输出，再带 `COREHOST_TRACE` 跑一次，并写 `/sdcard/Android/data/org.ew4.game/files/diagnose.txt`。
3. `--probe` 阶段：runtime → io → sha256 → socket → aspnet-build → aspnet-start → aspnet-request → `probe: done`；崩溃时最后一行即出事阶段。

经验：exit 139 且日志为空 = 崩在托管代码前（看 host 跟踪最后一行）；`SIGABRT` + “No usable version of libssl” = 用了系统加密。

## 验证与交付

- 桌面可验证：`dotnet run --project src/Ew4.Backend.Web -- --probe`、`EW4_FORCE_MANAGED_SHA256=1` 下启动/存档/读档。
- 真机行为无法在本机验证：交付 APK 时明确告知用户“未在真机验证”的部分。
- APK 体积主要来自 mods（base≈164MB、ww1≈151MB…）；首次启动要解压，较慢属预期。
