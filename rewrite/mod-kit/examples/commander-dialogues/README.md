# 将领专属台词 JSON

本目录是可打包的作者覆盖包。入口 `rewrite/extension.json` 用 `schema:1 / apiVersion:2` 的 `commanderDialogues` 引用台词文件；已有扩展时只合并这一字段。需要包含本接口的新版引擎，旧引擎可能拒绝未知字段。

修改 `rewrite/presentation/commander-dialogues.json`，在 `commanders` 对象中按将领定义 ID 添加条目：

```json
{
  "schema": 1,
  "commanders": {
    "ew4.commander.1": {
      "kill": ["士兵们，继续前进！", "胜利属于我们的军队！"],
      "occupy": [],
      "retreat": []
    }
  }
}
```

- `ew4.commander.N` 对应 `def_commander.xml` 的 `id="N"`，不是战局内单位 ID、玩家持有 ID、译名或头像名。新增将领完成原资源定义并重新导入后，同样可在这里配置；只有台词 JSON 不会创建将领或头像。
- `kill`：收到现有击杀喊话通知时，从该将领的数组中等概率选一句，替代通用6句；未配置将领、缺少该字段或空数组时回退通用台词。玩家派遣与剧本内将领均使用定义 ID。
- `occupy`、`retreat`：预留数组，目前不产生额外占领或退场触发。
- 所有文本是直接显示的纯文字，不是文字表键。每类最多128句，每句1至2000字符，纯空白、null、重复键、未知字段和不存在的将领 ID 会拒绝加载。省略字段等同空数组。

原版同一人物可能有多个剧本定义 ID，即使名字、头像相同也分别匹配。模板同时演示拿破仑的 1 和第一征服使用的 20104；要覆盖其他剧本版本，追加相应 ID。客户端单位数据的 commanderDefinitionId 可用于核对实际匹配 ID。

## 条件击杀台词

在将领条目中添加 `killRules`。每次该将领实际击杀部队后检查，包括击杀敌方将领、最后一支部队、反击、溅射和双方阵亡；不受原通用喊话的8%抽样限制。战斗伤害和战斗随机数序列保持原样，条件规则的概率与随机选句仅用于表现。

```json
"killRules": [
  {
    "id": "defeat-ushakov",
    "when": {
      "victimCommanders": ["ew4.commander.9501"],
      "scenarios": ["conquest1"],
      "minRound": 1,
      "maxRound": 10000,
      "killerHealthPercentAtMost": 100
    },
    "chance": 100,
    "selection": "first",
    "lines": ["乌沙科夫的部队已被击溃，继续前进！"]
  }
]
```

规则按数组顺序检查，第一条满足全部条件且通过概率判定的规则生效。每次击杀最多选中一条，同一目标的普通喊话被替代；概率未通过则继续检查后续规则。没有规则生效时保留原来的 `kill`/通用喊话行为，不会强制增加一次普通喊话。

| 字段 | 含义与默认值 |
| --- | --- |
| `id` | 此将领下唯一的规则标识，必填 |
| `chance` | 0至100整数百分比，默认100；100必触发，0停用 |
| `selection` | `first`固定使用第一句；`random`从数组等概率选一句，默认random |
| `lines` | 必填，1至128句，沿用单句2000字符限制 |
| `when.victimCommanders` | 被击杀将领的完整定义 ID 数组；空或省略表示不限，数组内任一 ID 匹配即可 |
| `when.victimCategories` | 目标兵种类别数组：infantry/cavalry/artillery/naval/fort；空或省略表示不限 |
| `when.victimHasCommander` | true只匹配带将领部队，false只匹配无将领部队，省略表示不限 |
| `when.scenarios` | 剧本 ID 数组，空或省略表示不限 |
| `when.minRound` / `maxRound` | 包含边界的回合范围，默认1至2147483647 |
| `when.killerHealthPercentAtMost` | 击杀方剩余生命百分比上限，0至100整数，默认100；取本次结算后的血量，已移除的部队按0计算 |

不同条件字段之间是 AND，同一数组内部是 OR。`when` 可省略或使用 `{}` 表示不加限制。将领定义引用、字段拼写、概率、选句模式、回合范围、文本及重复规则 ID 都会校验；目标将领 ID 同样需要已存在于当前导入资源中。指定目标将领时不能同时填写 `victimHasCommander:false`。

在作者副本修改后，用 Mod 工具 `pack`、`verify`、`import` 并启用。文件路径为包内相对路径，必须在 `rewrite/` 内，不能使用绝对路径或 `..`；无实时热加载。电脑和 Android 共用加载与校验。配置文件沿用扩展指纹和 Mod 账户隔离，改配置后按工具生成的新内容版本开局，旧档保留。

```powershell
.\Ew4.ModTool.exe pack "D:\mods\my-dialogues" --id=my-dialogues --name="将领台词" --version=1.0
.\Ew4.ModTool.exe verify "D:\mods\my-dialogues"
.\Ew4.ModTool.exe import "D:\mods\my-dialogues"
```
