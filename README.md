# EW4 Rewrite

《欧陆战争4》新架构重写（分支 `refactor/pack-architecture`）。**玩法与内容在 `mods/`；引擎与宿主在 `src/`。**

| 文档 | 说明 |
|------|------|
| [docs/arch/README.md](docs/arch/README.md) | 项目分层与 HTTP 接口 |
| [mods/README.md](mods/README.md) | 怎么写 Mod、差异包目录 |
| [install/README.md](install/README.md) | **发行目录**（只拷 `install/` 也能玩） |
| [docs/task/todo.md](docs/task/todo.md) | 回归门禁 G1–G6 |
| [docs/README.md](docs/README.md) | 文档总索引 |

## 发行与 Mod（已实现）

**最终发布形态是 `install/` 目录**：拷贝整个 `install/`（至少含 `web/app` 的 publish、`web/mods/base`、`web/user`）即可在装有 .NET 运行时的机器上游玩，**不需要**本仓库的 `src/` 或 `Ew4.sln`。在仓库内执行 `scripts/ew4.sh install` 会生成 `install/web/app` 与 `install/web/run.sh`；详见 [install/README.md](install/README.md)。

**Mod 切换已实现**：Web 主界面左下角 **Mod** 打开内容包面板，列出 `install/web/mods/` 下已安装包，点选即可 **激活**（写入 `install/web/user/active.json`，页面刷新后生效）。带 `parent: "base"` 的差异包会与 `base` 按文件路径合并加载。也可在面板内 **导入** `.zip` / `.ew4mod`（`POST /api/mod-import`）。作者维护包在仓库 `mods/<id>/`，用 `scripts/ew4.sh sync-mods` 同步到运行时目录。

---

下文在**仓库根目录**执行。需要 **Docker**（镜像内带 .NET 10 SDK，宿主机不必装 SDK）。可选封装脚本：`scripts/ew4.sh`（见文末）。

---

## 1. 获取代码

```bash
git clone https://github.com/TheWindISeek/ew4-rewrite.git
cd ew4-rewrite
```

（SSH：`git@github.com:TheWindISeek/ew4-rewrite.git`）

准备内容包 **`mods/base/`**（根清单 **`mod.json`**、`scenarios/`、`assets/` 等，不进 Git；字段见 [mods/README.md](mods/README.md)）。有种子数据时可在镜像里跑 Tool 的 `import-base`（见 §5）。

---

## 2. 构建开发镜像（首次）

```bash
docker compose -f .config/docker/compose.yaml build dev
```

得到镜像 **`ew4-dev:local`**（`Dockerfile.dev` → `mcr.microsoft.com/dotnet/sdk:10.0`）。

**Windows / macOS：** 安装 [Docker Desktop](https://www.docker.com/products/docker-desktop/)，在 **PowerShell** 或 **cmd** 里执行上述命令即可，不依赖 `ew4.sh`，也不要求单独配置 WSL。

**Linux：** 安装 Docker Engine + Compose 插件。

---

## 3. 同步内容包到运行时目录

运行时读 **`install/web/mods/`**（APK 渠道为 **`install/apk/mods/`**），维护在 **`mods/`**。推荐：

```bash
scripts/ew4.sh sync-mods
```

或手动：

```bash
mkdir -p install/web/mods install/web/user install/apk/mods install/apk/user
cp -a mods/. install/web/mods/
cp -a mods/. install/apk/mods/
```

---

## 4. 编译

在仓库根挂载为容器内 `/src`：

```bash
docker run --rm -v "$(pwd):/src" -w /src ew4-dev:local \
  dotnet build Ew4.sln -c Release
```

**PowerShell：**

```powershell
docker run --rm -v "${PWD}:/src" -w /src ew4-dev:local dotnet build Ew4.sln -c Release
```

输出在各项目的 `build/`、`bin/`、`obj/`（由仓库 MSBuild 属性决定）。

---

## 5. 测试

```bash
docker run --rm -v "$(pwd):/src" -w /src ew4-dev:local \
  dotnet test Ew4.sln -c Release --no-build
```

（若未先 build，去掉 `--no-build`。）

单项目示例：

```bash
docker run --rm -v "$(pwd):/src" -w /src ew4-dev:local \
  dotnet test tests/Ew4.Test.Platform.Content/Ew4.Test.Platform.Content.csproj -c Release
```

**内容包校验**（需已同步 `mods/base` 到 `install/web/mods/base`）：

```bash
docker run --rm -v "$(pwd):/src" -w /src ew4-dev:local \
  dotnet run --project src/Ew4.Tool/Ew4.Tool.csproj -c Release -- validate smoke
```

其它 Tool 子命令：`check btl`、`check json-btl`、`import-base` 等同理，把参数接在 `--` 后面。

---

## 6. 运行 Web

端口默认 **5184**（可改 `-p` 与 `--urls`）。

```bash
docker run --rm -it \
  -e EW4_HOME=/src/install/web \
  -v "$(pwd):/src" -w /src \
  -p 5184:5184 \
  ew4-dev:local \
  dotnet run --project src/Ew4.Backend.Web/Ew4.Backend.Web.csproj -- \
  --root /src \
  --home /src/install/web \
  --urls "http://0.0.0.0:5184"
```

浏览器打开：**http://127.0.0.1:5184**

**PowerShell** 把 `$(pwd)` 换成 `${PWD}`。

开发态用 `scripts/ew4.sh web` 时，Mod 面板同样可用（读写 `install/web/mods` 与 `install/web/user`）。

**仅 `install/` 发行包**（已 `install` publish 过）：

```bash
./install/web/run.sh
# 浏览器 http://127.0.0.1:5184 → 左下角 Mod 切换内容包
```

---

## 7. 环境变量（容器内 / 宿主）

| 变量 | 默认 | 含义 |
|------|------|------|
| `EW4_HOME` | 容器内建议 `/src/install/web` | Web 渠道 `mods`、`user` |
| `EW4_CHANNEL` | `web`（默认）或 `apk` | 未设 `EW4_HOME` 时的默认安装根 |
| 仓库根 `--root` | `/src` | 代码与 `mods/` 维护目录 |

更细的目录说明：[docs/development/environment.md](docs/development/environment.md)。

---

## 8. 不用 Docker：本机 .NET SDK

安装 [.NET 10 SDK](https://dotnet.microsoft.com/download) 后，在仓库根：

```bash
dotnet build Ew4.sln -c Release
dotnet test Ew4.sln -c Release
# 先 §3 同步 mods → install/web/mods
dotnet run --project src/Ew4.Backend.Web/Ew4.Backend.Web.csproj -- \
  --root . --home install/web --urls http://127.0.0.1:5184
```

适合 Windows 上不想开 Docker、直接 Visual Studio / `dotnet` 开发的场景。

---

## 9. 仅运行 Windows 发布包（不编译）

若已有 **`build/release-*/web/`** 下的 Windows Web ZIP（含 `Ew4.Backend.Web.exe`），可用 [scripts/windows/Start-Web.cmd](scripts/windows/Start-Web.cmd) 启动，与 Docker 无关。

---

## 可选：`scripts/ew4.sh`

Linux / Git Bash 下的快捷封装（内部仍是 `docker compose` + `dotnet`）。例如：

```bash
scripts/ew4.sh build -c Release
scripts/ew4.sh test -c Release
scripts/ew4.sh web
```

完整列表：`scripts/ew4.sh help`、[scripts/README.md](scripts/README.md)。

---

## 仓库目录（摘要）

```text
mods/  ──同步──►  install/web/mods/  ──►  HostApp（Web）
         └──►  install/apk/mods/     （APK 渠道，独立 user）
                      ▼
           Ew4.Service（/api/*）
                      ▼
           Ew4.Backend.Web + Ew4.Front.Web
```

| 路径 | 作用 |
|------|------|
| `src/` | 解决方案主体 |
| `tests/` | `Ew4.Test.*` |
| `install/web/` | Web 渠道：`app/`、`mods/`、`user/` |
| `install/apk/` | APK 渠道（与 Web 隔离） |
| `maps/` | 策划 JSON（进 Git） |
