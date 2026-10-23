# M0：统一包清单为 mod.json

状态:**已落地**  
关联：[roadmap.md](./roadmap.md) M0

## 结论

- 作者只维护 **`mod.json`**（包根一个文件）。
- 内含：**身份 / 继承 / 引擎要求 / formats**，以及扩展段 **`scenarios`**（剧本加载策略）。
- **`pack.json` 已废弃**：有 `mod.json` 则只用 `mod.json`；没有时才只读 `pack.json`（不报错、不比对两者）。

## 代码入口

| 主题 | 位置 |
|------|------|
| 路径常量 | `PackManifestPaths` |
| 解析 | `PackManifestReader` |
| `scenarios` 段 | `PackModProfile`、`PackManifestScenarios` |
| 工具写入 | `PackModProfile.WriteEmbeddedJsonProfile` |

字段说明：[mods/README.md](../../../mods/README.md)。
