# Ew4.Test.Front → Ew4.Front.Web

浏览器前端的 **Vitest 单元测试**（Node，无浏览器）。与 `Ew4.Test.*` 的 C# 项目并列；**不是** csproj，不进 `scripts/ew4.sh test`（dotnet）。

```bash
scripts/ew4.sh front-test
```

被测脚本在 `src/Ew4.Front.Web/wwwroot/js/`；`load-wwwroot-script.js` 从该目录加载模块。
