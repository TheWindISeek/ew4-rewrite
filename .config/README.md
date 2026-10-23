# .config — 工具链与容器配置

| 路径 | 说明 |
|------|------|
| [dotnet-tools.json](dotnet-tools.json) | `dotnet tool restore`（`scripts/ew4.sh fmt`） |
| [docker/](docker/) | `Dockerfile.dev`（日常开发）、`Dockerfile`（发行 Web）、`compose.yaml` |
| [editorconfig](editorconfig) | C# 命名与格式规则的**副本**；生效文件仍为根目录 [.editorconfig](../.editorconfig) |

.NET SDK 版本由 **`docker/Dockerfile.dev`** 的 `FROM mcr.microsoft.com/dotnet/sdk:10.0` 固定，不再使用 `global.json`。

`.dockerignore` 必须在仓库根（构建 context 为仓库根时 Docker 只读该路径）。
