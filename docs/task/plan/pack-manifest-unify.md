# 合并 pack.json 与 mod.json

状态:**待办**  
关联路线图：[roadmap.md](./roadmap.md) **M0**

## 背景

当前每个内容包根目录有两份清单：

| 文件 | 解析 | 职责 |
|------|------|------|
| `pack.json` | `PackManifestReader` | 安装身份：id、name、version、`parent`、`requires`、`formats`（**必填**） |
| `mod.json` | `PackModProfile` | 剧本策略：`scenarios.storage`、`documentSchema`（**可选**，推荐） |

作者与工具链容易漏写、字段分散；`import-base` 写嵌入 JSON 但**未**自动写 `mod.json`（依赖 Auto）。

## 目标

- **一个根清单文件**（可仍叫 `pack.json` 或改名，待决）包含：现有 `pack.json` 字段 + `scenarios` 存储策略（及后续 Mod 元数据扩展位）。
- 读取层：`PackManifestReader` / `PackModProfile` 合并或单一类型；**兼容期**仍接受旧包双文件。
- 工具：`import-base`、`import-apk-overlay`、`pack-convert-legacy-scenarios`、zip 导入校验统一产出新形状。
- 文档：[mods/README.md](../../../mods/README.md) 只描述合并后的结构。

## 验收（草案）

1. 新字段在合并清单中有 schema 版本号；单测覆盖解析与缺省（等价于今日 Auto / embedded-json）。
2. `PackInstallGuard`、Web 导入对旧双文件包仍可通过（或提供一次性迁移 Tool）。
3. `mods/README.md` 与实现对齐；G3 smoke + `test-mod-switch` 通过。

## 非目标（本项不做）

- 不要求一步删掉包内 legacy XML 表路径。
- 不合并 `client/countries.json` 等进根清单（仍放 `assets/client/`）。
