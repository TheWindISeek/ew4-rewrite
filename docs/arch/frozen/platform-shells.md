# 外壳与平台（冻结）

## 外壳

| 外壳 | 形态 | 说明 |
|------|------|------|
| Web | 浏览器访问 `Ew4.Backend.Web` | 主要开发与调试形态；**不再做第二套桌面壳** |
| Android（路线 A） | Java 壳 + WebView，APK 内嵌后端进程 | 复用同一份前端与后端；**不另写原生 UI** |

- 前端永远只有一份：`src/Ew4.Front.Web/wwwroot`。Android 外壳**不得**分叉前端代码。
- 后续的“路线 C”（后端内嵌进程内/原生化）是未来工作，当前不做。

## Android 约束

1. 后端作为子进程由 `ServerProcess` 启动，只监听 `127.0.0.1:<随机端口>`，用 `EW4_ACCESS_TOKEN` + cookie `ew4_token` 做访问控制（Backend 中间件，缺失/错误 403）。
2. 运行时是 **Mono**（`libcoreclr.so` 实为 Mono，无 `libclrjit.so`）。官方 `linux-bionic-arm64` 包在真机上初始化即崩，**使用 Termux 的同版本（10.0.13）Mono 覆盖**（`scripts/dev/android.sh` 的 `ew4_android_overlay_runtime`）。升级 .NET 版本必须同步核对 Termux 同版本，并在真机验证。
3. 无 OpenSSL：必须设置 `EW4_FORCE_MANAGED_SHA256=1`，所有哈希走 `Sha256Hash`。不要引入依赖系统加密库的功能（HTTPS、DataProtection）。
4. 保持 `targetSdkVersion=28`：之后的版本禁止从应用数据目录 exec，后端进程方案会失效。
5. 启动自检（`--probe` + 环境档位）和屏幕诊断必须保留：用户拿不到设备日志时，这是唯一可观测手段。
6. APK 默认内置仓库 `mods/` 下全部 mod；`EW4_APK_MODS` 仅用于显式取子集。
7. 构建全程在 Docker（`scripts/ew4.sh apk`）；宿主机只需 Docker。

## 构建与环境

- .NET 只通过 Docker 的 dev 镜像使用（`scripts/ew4.sh build|test|install|web|apk`）。
- `docs/development/` 是维护者手册，AI 不改。

## 不允许

- 为 Android 单独分叉前端、API 或存档格式。
- 把后端改成需要 OpenSSL/ICU 等设备上不存在的系统库（当前 `InvariantGlobalization`）。
- 删除自检/诊断路径，或把诊断信息只写到用户无法访问的位置。
- 在没有真机验证的情况下升级/替换 Android 运行时。
