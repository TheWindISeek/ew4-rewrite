# Docker

| 文件 | 镜像 |
|------|------|
| **`Dockerfile.dev`** | **`ew4-dev:local`** — **唯一**开发镜像：SDK、Python/Node、Android SDK（`platforms;android-34`、`build-tools;34.0.0`）、**apktool 2.9.3**（`ARG APKTTOOL_VERSION`）；`build` / `test` / `web` / `export-apk` / `apk`（无独立 `ew4-android` 镜像） |
| `Dockerfile` | `ew4-web:local` — 发行 `Ew4.Backend.Web`（运行时，非开发） |

```bash
scripts/ew4.sh image dev          # 强制重建 ew4-dev:local
docker compose -f .config/docker/compose.yaml build dev
```

修改 `Dockerfile.dev` 后请 **`image dev`** 重建；`build`/`test` 仅在本地无镜像时自动 build（**不会**无缓存重编已有镜像）。

```bash
docker run --rm ew4-dev:local apktool --version   # 应为 2.9.3，勿用宿主机 PATH 里的旧 apktool
```

**构建耗时**：首次或 SDK 层失效时，`sdkmanager` 下载可能 **10+ 分钟**且 Dockerfile 将该步输出重定向到 `/dev/null`，终端可能长时间无新行，属正常。请直接看 `docker compose build` 的 step 推进，**不要**用 `tail` 管道截断日志。若 `DOCKER_BUILDKIT=1` 报缺少 buildx，去掉该环境变量，仅用 `scripts/ew4.sh image dev`。

排障详见 [docs/debug/export-apk-apktool-and-dev-image.md](../../docs/debug/export-apk-apktool-and-dev-image.md)。

构建 context 为仓库根；`.dockerignore` 在根目录。
