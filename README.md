# HPT-AdaptiveTraderUI

适用于 **SPT 4.1.6 / EFT 1.0.x（Unity 2022.3，BepInEx 5.4.23.5）** 的商人界面增强 BepInEx 插件。

把 **Kaeno-TraderScrolling**（顶部商人卡片条横向滚动 + 玩家信息面板美化）与
**AdjustableTraderRows**（商人选择列表可调每行数量）两个功能合并为一个插件。
本项目为独立实现，与上述项目的作者无隶属关系，详见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。

> **English summary**: A BepInEx plugin for SPT 4.1.6 / EFT 1.0.x that adds horizontal mouse-wheel
> scrolling to the top trader card strip, configurable merchants-per-row for the merchant selection
> list (with wrap/vertical scrolling), and optional player-card visual tweaks. It is an independent
> reimplementation inspired by Kaeno-TraderScrolling (MIT) and AdjustableTraderRows (Unlicense).

## 下载 / Download

- **最新发布版（推荐，普通玩家直接下载）**：
  [GitHub Releases](https://github.com/henryplaytime/HPT-AdaptiveTraderUI/releases/latest)
- 当前版本：**v5.0.0** — [发布说明](https://github.com/henryplaytime/HPT-AdaptiveTraderUI/releases/tag/v5.0.0)
- 发布包内包含：`BepInEx/plugins/HPT-AdaptiveTraderUI.dll`、安装说明、LICENSE、第三方许可、CHANGELOG、
  示例配置与 SHA256 校验值。
- 源码用户：直接按下方「构建」一节从源码编译。

## 插件身份（Forge / 排查用）

| 项 | 值 |
|---|---|
| GUID | `com.henryplaytime.adaptivetraderui` |
| BepInPlugin Mod Name | `henryplaytime-AdaptiveTraderUI` |
| 版本 | `5.0.0` |
| 程序集 | `HPT-AdaptiveTraderUI.dll` |
| 配置文件 | `BepInEx\config\com.henryplaytime.adaptivetraderui.cfg`（首次启动自动生成） |

> 旧版 v4.2.4 使用的是旧 GUID `com.kaeno.TraderScrolling`。v5.0.0 起更换为独立 GUID/名称；
> 两个版本的配置文件路径不同，旧配置不会自动迁移（如需保留设置请手动对照填写）。

## 特性

| 界面 | 位置 | 效果 |
|---|---|---|
| **界面 A：商人卡片条** | 进入商人后，屏幕顶部 Trade/Tasks/Services 标签上方那一排商人头像卡 | 商人超过约 10 个时，右侧卡片不再被玩家信息面板永久挡住：整条卡片可**鼠标滚轮横向滚动**；滚到最右端可看到最后一个商人 |
| **界面 B：商人选择列表** | 主界面点商人图标后进入的商人列表（原版一行约 5 个） | `Wrap`：一行 N 个、自动换行，超出高度时**纵向滚动**；`Horizontal`：全部排成一行、横向滚动；`None`：保持原版 |
| **玩家卡美化** | 商人界面右侧玩家信息面板 | 金额右移、间距调整、背景 Tile 激活加宽、背景变黑（可关闭） |

其他特性：

- **失败安全**：所有 patch 与 UI 修改都包在 `try/catch` 中；找不到节点时保持原版布局，只记日志，不会崩溃游戏。
- **旧插件检测**：启动时若发现 `BepInEx\plugins\Kaeno-TraderScrolling.dll` 仍存在，会拒绝启用 patch 并在日志中提示删除（两个插件修改同一套 UI，不能共存）。
- **诊断默认关闭**：仅在 `[Advanced] Diagnostic Dump = true` 时生成 `BepInEx\HPT-AdaptiveTraderUI-diag.txt`，正常游玩不会产生额外文件。
- **无网络通信 / 无数据收集 / 无广告 / 无支付要求 / 不修改存档与游戏文件。**

## 安装（使用发布版）

1. **完全关闭** SPT/EFT 游戏。
2. 删除或改名旧模组文件（必须，否则本插件会检测到并拒绝启用 UI 修改）：
   ```
   BepInEx\plugins\Kaeno-TraderScrolling.dll
   BepInEx\plugins\AdjustableTraderRows.dll（或 REVIVED 版）
   BepInEx\plugins\Kaeno-AdaptiveTraderUI.dll（如果你装过 v4.2.4 旧版）
   ```
   建议改名为 `.dll.bak` 移到别处备份。
3. 把发布包里的 `BepInEx` 文件夹解压到 **SPT 游戏根目录**（与 `EscapeFromTarkov.exe` 同级），
   最终得到：
   ```
   <SPT目录>\BepInEx\plugins\HPT-AdaptiveTraderUI.dll
   ```
4. 启动游戏。首次启动会自动生成配置：
   ```
   BepInEx\config\com.henryplaytime.adaptivetraderui.cfg
   ```
5. 进入商人界面，用鼠标滚轮在顶部卡片条上横向滚动验证。

## 构建（开发者）

### 准备

- Windows
- [.NET SDK](https://dotnet.microsoft.com/)（8.0 或更新；目标框架 `netstandard2.1`）
- 本地已安装 **SPT 4.1.6 + BepInEx 5.4.23.5** 的游戏目录

> 游戏/SPT/BepInEx 程序集只用于本地编译引用，**不会**提交到仓库或随发布包分发。

### 构建

```powershell
# 方式 1：命令行指定游戏目录
dotnet build -c Release -p:GameDir="D:\你的SPT目录\Escape From Tarkov_4.1.6"

# 方式 2：使用环境变量
$env:SPT_GAME_DIR = "D:\你的SPT目录\Escape From Tarkov_4.1.6"
dotnet build -c Release

# 方式 3：仓库自带脚本（产物会复制到 artifacts\；Windows PowerShell 5.1 / PowerShell 7 均可）
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build.ps1 -GameDir "D:\你的SPT目录\Escape From Tarkov_4.1.6"
```

产物：`bin\Release\HPT-AdaptiveTraderUI.dll`（脚本方式还会复制到 `artifacts\`）。

### 打包发布版

先构建，然后：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\package.ps1
```

生成 `artifacts\HPT-AdaptiveTraderUI-v<版本>.zip`（包含 DLL、README、LICENSE、第三方许可、更新记录、示例配置与 SHA256 校验值）。

## 配置

配置文件：`BepInEx\config\com.henryplaytime.adaptivetraderui.cfg`（首次启动自动生成）。

### `[Config]`

| 键 | 默认值 | 说明 |
|---|---|---|
| `Scroll Wheel Speed` | `30` | 鼠标滚轮滚动速度（每格像素，1–500）；横向/纵向都用它 |
| `Trader Strip Scrolling` | `true` | 界面A（顶部卡片条）是否启用横向滚轮滚动；`false` = 该界面完全原版 |
| `Use Elastic` | `true` | 界面A 滚动越界是否弹性回弹：`true` = 弹性，`false` = 硬性夹取（界面B 固定硬性夹取） |
| `Enable Player Card Tweaks` | `true` | 玩家卡/金钱面板美化开关 |

### `[Layout]`（作用于界面B：商人选择列表）

| 键 | 默认值 | 说明 |
|---|---|---|
| `Merchants Layout Mode` | `Wrap` | `Wrap`（一行 N 个、自动换行，超出高度纵向滚动）/ `Horizontal`（全部一行、横向滚动）/ `None`（不改造，保持原版） |
| `Merchants Per Row` | `8` | `Wrap` 模式每行商人数（1–20，自动 clamp 到实际商人数量） |
| `Trader Columns` | `0` | 旧模组键名兼容项：`0` = 忽略；`>0` 时覆盖 `Merchants Per Row` |

### `[Advanced]`

| 键 | 默认值 | 说明 |
|---|---|---|
| `Diagnostic Dump` | `false` | 诊断用：把界面A 的几何/滚动/计数自检写入 `BepInEx\HPT-AdaptiveTraderUI-diag.txt` |
| `Debug Logging` | `false` | 输出更详细的调试日志 |

示例配置见 [`config/com.henryplaytime.adaptivetraderui.cfg.sample`](config/com.henryplaytime.adaptivetraderui.cfg.sample)。

## 诊断

遇到滚动位置/卡片显示异常时：

1. 关闭游戏，把 `[Advanced] Diagnostic Dump` 改为 `true`。
2. 启动游戏，打开商人界面，把顶部卡片条滚到最右端，再修改一次任意设置。
3. 退出游戏，附上以下文件提交 Issue：
   - `BepInEx\LogOutput.log`
   - `BepInEx\HPT-AdaptiveTraderUI-diag.txt`
4. 诊断文件中的 `CHECK1`~`CHECK7` 会直接给出：
   - 滚到最右端时最后一张卡的位置 vs 可见边界；
   - 内容宽度 vs 卡片实际末端（是否存在“空白尾”）；
   - `childCount` / 实际排版数 / `_spawnedCards` 计数是否一致；
   - `ScrollRect` 的 `movementType`、viewport/content 目标、锚点是否被其它模组改动。

## 工作原理（简述）

完整说明见 [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)。要点：

- 界面A：在 `TraderCards` 上添加横向 `ScrollRect`（**不改变父级/锚点/遮罩**），并通过隐藏的
  `AdaptiveTraderCardsViewport`（`Container` 子节点，无 Graphic/Mask）作为滚动边界，宽度 = 卡片左边缘 → 右侧玩家面板左边缘。
  内容宽度按实际参与排版的卡片末端计算，滚到最右端时最后一张卡正好停在可见边界。
- 界面B：保留 `FlowLayoutGroup`，按配置行数换行，由插件精确控制内容高度后添加纵向 `ScrollRect`。
- 所有布局都在原版 UI 生成完成后的 HarmonyX `postfix` 中应用；失败时回退原版。

## 目录结构

```
src/
  Plugin.cs                      插件入口（BepInPlugin + 逐 patch 启用 + 旧 Kaeno 检测）
  Config.cs                      全部配置项与热更新事件
  Log.cs                         日志出口
  TraderCardsController.cs       界面A：顶部商人卡条横向滚动
  TraderCardsController.Diag.cs  界面A：诊断快照 + 逻辑自检（partial，仅 Diagnostic Dump 开启时生效）
  MerchantsListController.cs     界面B：商人选择列表换行/横向滚动
  PlayerCardTweak.cs             玩家卡美化
  UILayoutTools.cs               UI 层级/布局/几何工具
  DiagFile.cs                    诊断文件写入（默认关闭）
  DiagnosticDumper.cs            日志诊断 dump
  Patches/                       HarmonyX ModulePatch（三个界面入口）
scripts/
  build.ps1                      构建
  package.ps1                    打包发布 zip
docs/
  ARCHITECTURE.md                实现说明
  FORGE-SUBMISSION.md            Forge 发布前检查清单 / 表单字段参考
config/
  com.henryplaytime.adaptivetraderui.cfg.sample
```

## 已知限制

- 仅针对 **SPT 4.1.6 / EFT 1.0.x** 开发；其它游戏版本请勿混用。
- 与其它修改商人/交易界面的模组（例如 Tyfon.UIFixes、TraderSearch、TraderGen 等）未做逐一兼容验证。
  如出现异常，可先把 `Trader Strip Scrolling = false`、`Merchants Layout Mode = None` 回退原版。
- 旧版 v4.2.4 的配置路径为 `com.kaeno.TraderScrolling.cfg`；v5.0.0 使用新 GUID，配置不会自动迁移。
- 发布到 Forge 前需要取得上游作者的明确许可（见 [docs/FORGE-SUBMISSION.md](docs/FORGE-SUBMISSION.md)），
  本仓库的开源发布不等于 Forge 的授权要求已满足。

## 许可证与致谢

- 本项目代码使用 **MIT License**，见 [LICENSE](LICENSE)。
- 功能参考/合并自：
  - **Kaeno-TraderScrolling** — Copyright (c) 2024-2025 Kaeno (previously Naekami) and CWX，MIT License；
  - **AdjustableTraderRows** — public domain (Unlicense)，by JustNU；SPT 4.x 兼容版本由 Wrath_Jr 维护。
  - 完整第三方许可文本见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。
- 感谢 SPT 团队、BepInEx、HarmonyX 以及所有测试/反馈本插件的玩家。

## 免责声明

本项目与 Battlestate Games、SPT 项目及上述第三方项目的作者**无任何隶属或背书关系**。
「Escape from Tarkov」是 Battlestate Games 的商标。使用本插件需要你自行合法拥有《Escape from Tarkov》
并安装 SPT 与 BepInEx；请遵守相关游戏与平台的使用条款。
