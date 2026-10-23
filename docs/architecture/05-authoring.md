# 05 创作层(编辑器与实时预览)

状态:草案

目标:作者在网页或手机浏览器里完成"选内置地图 → 编辑剧本 → 校验 → 试玩 → 导出",**所见即所得**,不再靠"改文件、重新打包、重新导入"。

## 1. 为什么单独成层

- 创作需要的能力(复制包、改文件、校验、预览、导出)与"玩一局"不同。
- 创作**不能**占用玩家当前的局和存档(PRD FR-E05)。
- 它依赖 Core 和 Content,**不依赖 Session**(见 [01 依赖规则](01-overview.md#4-依赖规则))。

## 2. 核心概念:Workspace(草稿包)

草稿包就是一个 Pack,放在可写目录:

```text
workspaces/
  {workspaceId}/
    pack.json              parent 指向复制来源
    ...                    与 Pack 同结构
    .workspace.json        草稿元信息:创建时间、最近校验结果、导出历史
```

**新建 Mod = 复制。**

| 操作 | 效果 |
|---|---|
| 新建(从 base) | 复制 `base` 的目录结构,或只创建空目录并设 `parent: "base"` |
| 新建(从某 Mod) | 复制该 Mod,`parent` 指向其 `parent` |
| 编辑 | 修改草稿包内的文件 |
| 导出 | 校验到 `SmokeStart` 通过后,打成 `.ew4mod` |

"复制后修改"与"只放修改过的文件"都合法,因为 Content 层支持按 `parent` 补全(见 [03 §1.3](03-content-and-packs.md#13-单激活与补全))。

## 3. 接口(草案)

```csharp
public interface IWorkspaceService
{
    IReadOnlyList<WorkspaceInfo> List();
    WorkspaceInfo Create(string fromPackId, string newId, string name);   // 复制
    void Delete(string workspaceId);

    byte[] ReadFile(string workspaceId, string path);
    WriteResult WriteFile(string workspaceId, string path, byte[] data);  // 先过 ContentKind 校验,拒绝非法路径与超限

    IReadOnlyList<Diagnostic> Validate(string workspaceId, ValidationLevel upTo);
    ExportResult Export(string workspaceId, ExportOptions options);       // .ew4mod,可选附带 BTL/XML 兼容产物
}

public interface IScenarioEditor        // 面向剧本的结构化编辑
{
    ScenarioModel Open(string workspaceId, string scenarioId);
    EditResult Apply(string workspaceId, string scenarioId, ScenarioEdit edit);   // 放置势力/部队/建筑/事件
}
```

对应的 HTTP 路由前缀 `/api/author/...`(`workspaces`、`files`、`validate`、`export`、`preview`)。

**写入规则:**

- 每次 `WriteFile` 经过 `IContentKindRegistry.Match`:路径不属于任何已注册类型则拒绝。
- `extra`(未知字节块)不可编辑。
- 单文件和整包大小遵守 ContentKind 声明的上限。

## 4. 实时预览

**机制:单激活下的"切换到草稿"。**

```text
进入预览   Host.Activate(草稿包) —— 与启用 Mod 走同一条路径
           存档目录使用独立的 workspace 命名空间,不碰玩家存档
修改文件   草稿文件变化 → 重新 Resolve → 重建 SessionService → 前端刷新
退出预览   Host.Activate(之前的包)
```

- 不需要为预览单独发明一套运行机制。
- 预览产生的存档放在 `saves/ws-{workspaceId}/...`,不污染玩家档案。
- 解析耗时若成为瓶颈,再加增量缓存(只重新解析被改动的文件)。**先不做**。

**为什么能做到"校验与游戏一致":** 预览、导入、导出、启用使用同一个 `IContentValidator`,最高一级 `SmokeStart` 是真的调用 `IEngine.NewGame`。

## 5. 编辑范围

| 内容 | 阶段 | 说明 |
|---|---|---|
| 剧本:势力、部队、建筑、事件 | 第一批 | 结构化编辑 |
| 将领定义与头像 | 第二批 | PRD FR-E07 |
| 规则参数(`rules/*.json`) | 第二批 | 表单化,字段文档化 |
| 脚本 | 不做编辑器 | 作为文本文件编辑 |
| 地图 | 后期,独立立项 | 先"选已有地图 id",上传需 MapPolicy |

## 6. 前置门槛

编辑器的**保存功能**依赖 Exporter 正确。因此:

1. 先完成导入/导出的往返验收(见 [03 §6](03-content-and-packs.md#6-格式转换importers-与-exporters)与 [08 剧本管线](08-scenario-edit-pipeline.md))。
2. 再做剧本编辑器的保存（经 `ScenarioBtlCodec` / `IScenarioEditService`）。

## 7. 移动端

- 同一套网页界面,Android WebView 可访问。
- 横屏优先;触控操作(拖动、缩放)与桌面鼠标操作分别适配。
- 性能指标(PRD NFR-05)后续定:先定义"单剧本规模"的格数、部队数上限,再测。

## 8. 与旧工具的关系

旧 `ModTool` 的命令(`import`、`list`、`export`、`pack`、`verify`、`*-export`、`*-apply`、`*-strip`)是创作层的命令行雏形。
新实现里,这些命令基于 `IPackRepository` / `IContentValidator` / Exporter 重写成 `Ew4.Tool`,**不再依赖游戏宿主**。
