# export-apk：开发镜像 apktool 与 Manifest 解码

| 字段 | 值 |
|------|-----|
| 日期 | 2026-10-27 |
| 状态 | 已修复 |
| 影响 | `scripts/ew4.sh export-apk`；`ex-commander-skills` 等完整 Manifest 补丁导出 |
| 关联任务 | plan H3（[apk-export.md](../task/plan/apk-export.md)） |

## 现象

1. **apktool 重建失败（旧工具链）**  
   在仅含 Debian 自带 **apktool 2.7** 的环境里，对 `data/shell/ew4-base.apk` 做 decode → 改 `assets/` → `apktool b` 时，**aapt 报错**，无法产出 unsigned APK。

2. **Manifest XML 解析崩溃（`-r`  workaround 的副作用）**  
   为规避 2.7 的 aapt 问题，decode 曾加 **`-r`（`--no-res`）**。导出在改 Manifest 阶段直接退出，例如：
   ```text
   System.Xml.XmlException: '…', hexadecimal value 0x03, is an invalid character. Line 1, position 1.
      at Ew4.Tool.Export.ApkManifestXmlEditor.ReadPackageName(...)
   ```
   apktool 日志对应 **`Copying raw manifest...`**，磁盘上的 `AndroidManifest.xml` 为 **二进制 AXM**，不是 UTF-8 XML。

3. **镜像版本与构建观感**  
   - `docker run --rm ew4-dev:local apktool --version` 仍为 **`2.7.0-dirty`** 等旧版本，说明 **`ew4-dev:local` 未按当前 `Dockerfile.dev` 重建**（仍带 Debian 包或中断前的层）。  
   - `./scripts/ew4.sh image dev` 在 **安装 Android SDK** 的 `RUN` 上可能 **十余分钟几乎无日志**（`sdkmanager` 输出被重定向到 `/dev/null`），易被误判为卡死。  
   - 用 **`… \| tail`** 盯构建会缓冲/截断 compose 输出，更难判断进度。  
   - 本机若启用 `DOCKER_BUILDKIT=1` 但 **未装 buildx**，会报 `BuildKit is enabled but the buildx component is missing`；应直接 `scripts/ew4.sh image dev`（legacy builder）。

## 原因

### A. 工具版本与 aapt

**apktool 2.7** 对当前壳 APK 的 **全量 decode + rebuild** 在 aapt 阶段失败；需要镜像内固定 **apktool 2.9.3**（见 `.config/docker/Dockerfile.dev` 的 `APKTTOOL_VERSION`）。

### B. `-r` 与 Manifest 编辑链路不兼容

`export-apk` 的 Manifest 补丁走 **`ApkManifestXmlEditor`**（`System.Xml.Linq` 读写 `package` / `versionCode` / `versionName`）。  
**`-r` 会保留 raw Manifest**（与 `res/` 一样不解码为 XML），与上述实现 **互斥**。

### C. 单一开发镜像

Android 壳构建与 `export-apk` 共用 **`ew4-dev:local`**（已删除独立 `Dockerfile.android` / `ew4-android:local`）。改 Dockerfile 后必须 **`image dev`**，不能假设 `build` 会自动无缓存重建。

## 修复

| 改动 | 文件 | 说明 |
|------|------|------|
| 镜像内 apktool 2.9.3 | `.config/docker/Dockerfile.dev` | 从 GitHub 安装 jar + `/usr/local/bin/apktool` 包装脚本 |
| decode 去掉 `-r` | `src/Ew4.Tool/Export/ApkToolRunner.cs` | 全量 decode，Manifest 为可读 XML；依赖 apktool **2.9+** |
| 文档与排障索引 | `docs/task/plan/apk-export.md`、`.config/docker/README.md`、`docs/debug/README.md` | 工具链与验证命令 |

**不要**再在 decode 上加 `-r` 作为「兼容 2.7」手段；应升级镜像内的 apktool。

## 验证

```bash
scripts/ew4.sh image dev
docker run --rm ew4-dev:local apktool --version    # 期望：2.9.3

scripts/ew4.sh export-apk \
  --out build/export-test/ex-commander-skills.apk \
  --mod ex-commander-skills --allow-lossy --export-id 1730000300
```

期望：

- stdout 摘要形如 `export-apk: out=… exportId=… filesPatched=… compat=…`
- `--out` 路径下 APK 存在（约数十 MB，随壳与补丁而定）

可选清理旧镜像：`docker rmi ew4-android:local`（若本地仍存在）。

- [ ] `apktool --version` 在 **同一镜像** `ew4-dev:local` 内为 2.9.3  
- [ ] 至少一个带 Manifest 改包名的 mod（如 `ex-commander-skills`）导出成功  

## 相关代码

- `src/Ew4.Tool/Export/ApkToolRunner.cs` — apktool decode/build  
- `src/Ew4.Tool/Export/ApkPackagingPipeline.cs` — decode → 补丁 assets → Manifest → build  
- `src/Ew4.Tool/Export/ApkManifestXmlEditor.cs` — Manifest XML  
- `scripts/dev/docker.sh` — `ew4_build_dev_image` / 容器内执行 export-apk  
