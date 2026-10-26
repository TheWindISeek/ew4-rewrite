# Ew4.Android

Android 壳：Java（无 Gradle）+ WebView，APK 内带自包含的 .NET 后端（linux-bionic-arm64）。
设计与限制见 [docs/task/plan/android-shell.md](../../docs/task/plan/android-shell.md)。

| 文件 | 作用 |
|------|------|
| `AndroidManifest.xml` | targetSdk 28（允许执行 `filesDir` 里的后端）、横屏、全屏 |
| `java/org/ew4/ServerProcess.java` | 解压 `assets/ew4-runtime.zip` / `ew4-mods.zip`，启动并等待后端 |
| `java/org/ew4/MainActivity.java` | 全屏 WebView，cookie token，文件选择器，进程被回收时重启 |
| `build.sh` | 在 `ew4-android` 容器里：aapt2 → javac → d8 → zipalign → apksigner |

构建：`scripts/ew4.sh apk` → `install/apk/ew4.apk`（debug 密钥签名，可直接侧载）。
