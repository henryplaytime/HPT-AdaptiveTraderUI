# 贡献指南 / Contributing

感谢你愿意改进 Kaeno-AdaptiveTraderUI。

## 反馈问题

1. 先确认游戏中只安装了本插件（旧的 `Kaeno-TraderScrolling.dll` 必须删除或改名，否则两个插件 GUID 相同会冲突）。
2. 打开 `BepInEx\config\com.kaeno.TraderScrolling.cfg`，把 `[Advanced] Diagnostic Dump = true`。
3. 复现问题后，把 `BepInEx\LogOutput.log` 和 `BepInEx\Kaeno-AdaptiveTraderUI-diag.txt`（如果存在）一起附在 Issue 里，并写明：
   - SPT / EFT 版本（例如 SPT 4.1.6 / EFT 1.0.x）
   - 本插件版本
   - 商人数量、商人选择列表每行设置
   - 复现步骤与预期/实际行为

## 提交代码

- 本仓库采用 **MIT** 许可证，提交 PR 即表示你同意以 MIT 许可证发布你的贡献。
- 不要提交任何游戏文件或反编译产物：`EscapeFromTarkov_Data`、`Assembly-CSharp.dll`、`BepInEx` 程序集、`Spt` 服务端文件等一律不要放进仓库。
- 不要提交 `bin/`、`obj/`、`dist/`、`*.zip`；构建产物由 CI/Release 生成。
- 代码风格：
  - C#（netstandard2.1，尽量兼容 C# 7.3+ 语法），4 空格缩进；
  - 所有对游戏对象的访问必须有 `null` 判断与 `try/catch`，保证插件失败时游戏仍可正常游玩；
  - 新增 UI 改动优先做成「失败安全 + 可回退到原版」。
- 改动界面逻辑时，请同时更新 `CHANGELOG.md` 与配置项说明（`README.md` / `src/Config.cs`）。

## 开发环境

见 `README.md` 的「构建」一节。需要一个安装好 SPT 4.1.6 + BepInEx 5.4.23.5 的游戏目录用于引用程序集，
但**游戏目录内容不会被提交**。

## 本地验证

```powershell
# 构建（把路径换成你自己的 SPT 安装目录）
dotnet build -c Release -p:GameDir="D:\SPT\Escape From Tarkov_4.1.6"

# 或者先设置环境变量
$env:SPT_GAME_DIR = "D:\SPT\Escape From Tarkov_4.1.6"
dotnet build -c Release
```

启动游戏验证后，建议把 `[Advanced] Diagnostic Dump` 打开一次并检查 `BepInEx\Kaeno-AdaptiveTraderUI-diag.txt`
里的 `CHECK1`~`CHECK7` 是否均为 PASS。
