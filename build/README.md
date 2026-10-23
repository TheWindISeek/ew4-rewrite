# build/ — MSBuild 编译输出

各 `src/`、`tests/` 项目的 **bin** 与 **obj** 集中在 `build/<项目名>/`（见仓库根 `Directory.Build.props`）。

**不要**把 Mod 内容、存档或安装包放在此目录；运行时使用 [`install/`](../install/README.md)（`EW4_HOME`）。
