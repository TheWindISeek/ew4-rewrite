# EW4 Rewrite

《欧陆战争4》新架构重写（分支 `refactor/pack-architecture`）。**玩法与内容在 `mods/`；引擎与宿主在 `src/`。**

- 架构与术语：[docs/README.md](docs/README.md)
- 开发环境：[docs/development/environment.md](docs/development/environment.md)
- 进度快照：[docs/development/todo.md](docs/development/todo.md)
- Cursor Agent：[.cursor/skills/ew4-dev/SKILL.md](.cursor/skills/ew4-dev/SKILL.md)

## 环境

只需 **Docker**。不在宿主机安装 .NET；SDK 版本由 **`.config/docker/Dockerfile.dev`** 基础镜像 `mcr.microsoft.com/dotnet/sdk:10.0` 固定。

## 快速开始

1. 准备 **`mods/base/`**（原版 Pack：`pack.json`、`scenarios/`、`assets/` 等；目录不进 Git，本机自行维护）。
2. 编译并起 Web：

```bash
scripts/ew4.sh build -c Release
scripts/ew4.sh web          # Ew4.Backend.Web，http://127.0.0.1:5184
```

首次 `build` / `web` 会自动构建 dev 镜像 `ew4-dev:local`。子命令与 `dev/` 模块说明见 **`scripts/ew4.sh` 文件头** 与 [scripts/README.md](scripts/README.md)。

**Mod**：在 `mods/` 下增加 `<id>/` 与 `base` 同级，或通过浏览器 / API 安装（`/api/mod-import` 等）。**没有**单独的 shell「import mod」脚本。

## 运行栈（当前）

```text
mods/  ──copy──►  install/mods/  ──►  Ew4.Platform.Runtime（激活 Pack）
                              │
                              ▼
                      Ew4.Platform.Session + Ew4.Service（/api/*）
                              │
                              ▼
                      Ew4.Backend.Web（Front.Web + /content/* + /api/author/*）
```

- 存档与当前包：`install/user/`（`EW4_HOME`，默认 `install/`）
- 策划 JSON：`maps/`（进 Git）
- 封存对照：`backup/`（不参与编译，勿引用其 csproj）

## 仓库目录

| 路径 | 作用 |
|------|------|
| `src/` | `Ew4.Platform.Engine`、`Ew4.Platform.Content`、`Ew4.Platform.Session`、`Ew4.Platform.Runtime`、`Ew4.Service`、`Ew4.Interface`、`Ew4.Front.Web`、`Ew4.Backend.Web`、`Ew4.Editor`、`Ew4.Tool` … |
| `tests/` | 单元测试 `Ew4.Test.*`（见 [tests/README.md](tests/README.md)） |
| `scripts/ew4.sh` | 唯一开发入口（容器内 `dotnet`） |
| `mods/` | 内容包维护目录（仅 `mods/README.md` 进 Git）；运行时镜像在 `install/mods/` |
| `maps/` | 地图等策划数据 |
| `build/` | MSBuild 输出（`bin`/`obj`） |
| `install/` | 发行 publish（日常开发可忽略） |
| `.config/` | `dotnet-tools.json`、`Dockerfile.dev`、compose |
| `docs/` | 架构、ADR、开发说明 |
| `backup/` | 旧代码只读封存 |
