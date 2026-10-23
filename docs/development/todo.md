# 重构待办与进度

状态:进行中（**第二阶段：做减法 / 安装布局 / 包规范**）  
分支:`refactor/pack-architecture`  
最后更新:2026-10-23

**权威方案:** [architecture/11-simplification-and-layout.md](../architecture/11-simplification-and-layout.md)  
**布局与脚本:** [build-install-layout.md](build-install-layout.md)、[scripts/README.md](../../scripts/README.md)  
**测试命名:** [tests/README.md](../../tests/README.md)（`Ew4.Test.*` 对齐 `src` 程序集名）

> 日常命令以 `scripts/ew4.sh` 文件头与 `help` 为准。宿主机只需 Docker，**不要**本机装 .NET 做 build/test。

---

## 程序集分层（当前 `src/`）

```text
Ew4.Front.Web              表现层（静态 UI）
Ew4.Backend.Web            接入层（Kestrel、/api/*、DI）
Ew4.Service                服务层（游玩 / Mod 编排）
Ew4.Platform.*             平台层（Engine → Content → Session → Runtime）
Ew4.Editor / Ew4.Tool      创作、离线 CLI（`Ew4.Tool.Import` = 包导入，非 Content API）
Ew4.Interface              跨层契约（`Interface.Front` / `Interface.Service` …）
```

---

## 第二阶段执行顺序（§11 文档 §10）

每步单独提交；每步完成后跑 **G1–G6**（§9），与 S0 基线对比。

| 步骤 | 内容 | 状态 | 完成标准（摘要） |
|------|------|------|------------------|
| **S0** | 记录基线 G1–G5 | ✅ | `docs/development/baseline-phase2.md` |
| **S1** | 切断 APK、`Ew4.Mods` | ✅ | 无 APK 路径 |
| **S2** | 删旧栈 + Lua/API1(D8) | ✅ | sln 无 Legacy/Mods 等 |
| **S3** | 安装布局 `install/`、`InstallLayout` | ✅ | 仅 `EW4_HOME` |
| **S4** | `Ew4.Interface` + 早期改名 | ✅ | 原 Contracts；PlayApi→服务层 |
| **R5** | §7.3 + Platform 命名 | ✅ | `Original*`、`assets/tables` |
| **分层** | Front / Backend.Web / Service / Platform | ✅ | 见上表；`Ew4.Interface` 替代 Contracts |
| **S5** | 剧本仅 JSON、内部 BTL | ✅ | G6：`check json-btl`；运行时 JSON→BTL；`pack-embed-scenarios` 迁移旧包 |
| **S6** | `pack.json`、`parent` P1–P8 | ✅ | `PackInstallGuard`（导入/启用/删除）；P2/P4–P8 拒绝路径单测 |
| **S7** | 文档对账 §11 | ✅ | `03`/`04`/`08`/`10`/`glossary`/`environment`/根 README 等与 mods↔install、Tool 导入对齐 |

---

## 回归门禁（每步必跑）

| # | 命令 | 通过标准 |
|---|------|----------|
| G1 | `scripts/ew4.sh build -c Release` | 0 error / 0 warning |
| G2 | `scripts/ew4.sh test -c Release` | 全绿 |
| G3 | `scripts/ew4.sh validate smoke` | `mods/base` SmokeStart |
| G4 | `scripts/ew4.sh check btl` | BTL 往返 |
| G5 | `scripts/ew4.sh web` → 浏览器 | 开局、回合、存读档、切包 |
| G6 | `scripts/ew4.sh check json-btl` | 嵌入 JSON→BTL 与参考语料一致 |

---

## 后续（第二阶段收尾 / 第三阶段前）

- 本地 APK 样例 Mod（`ww1` 等）全量 `validate smoke`、G5 切包烟测  
- `Ew4.Service` 内 `Client*` 类型名逐步改为域名词（非阻塞）  
- 补测试项目：`Ew4.Test.Platform.Engine`、`Ew4.Test.Service` 等  
- `docs/product/PRD.md` 仍含历史 APK/Lua 表述，**产品稿**，不与实现强绑定  

### 纪律

- **一步一提交**；改名提交不夹业务逻辑（§7.4）  
- 新跨层类型先进 **`Ew4.Interface`**，再实现  
- `Front.Web` **不**引用 `Ew4.Service` / `Ew4.Platform.*`  
- 路径用 `InstallLayout`、`RepositoryPaths`；内容包维护 **`mods/`**，运行时 **`install/mods/`**  
- 不要恢复应用内 APK、`Ew4.Mods`、Lua/API1  

### 明确不要做

- 不要引用 `backup/**/*.csproj`  
- 不要为新 Mod 加 Lua  
- 不要加 shell「import mod」（zip 走 HTTP；APK 走 `Ew4.Tool`）  

---

## 第一阶段（已完成摘要）

P0–P4：引擎、内容、会话、宿主、游玩 API、创作、Web、Cli MVP 已接（现名见分层表）。
