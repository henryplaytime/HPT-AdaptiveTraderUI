# 架构说明 / Architecture

> 本文件描述插件的模块划分与关键实现细节，便于二次开发。所有内容均为本项目的独立实现说明，
> 不包含游戏反编译源码。

## 1. 总体结构

插件入口 `src/Plugin.cs`（BepInEx `BaseUnityPlugin`，GUID `com.kaeno.TraderScrolling`）在 `Awake` 中：

1. 绑定配置（`src/Config.cs` → `ModConfig`）；
2. 逐项 `Enable()` 三个 HarmonyX `ModulePatch`：
   - `Patches/TraderScreensGroupPatch` → 界面A：`TraderScreensGroup.Show(TraderScreenController)` 之后；
   - `Patches/MerchantsListPatch` → 界面B：`MerchantsList.Show(...)` 之后；
   - `Patches/PlayerCardPatch` → 玩家卡：`DisplayMoneyPanelTMPText.Show()` 之后。
3. 每个 patch 单独 `try/catch`：任意一个失败不会影响其它功能；所有回调异常只记日志，不抛回游戏循环。

界面A 的逻辑放在 `TraderCardsController`（挂在 `TraderCards` GameObject 上），界面B 的逻辑放在
`MerchantsListController`（挂在 `Traders Container` 上），两者通过配置事件热更新。

## 2. 两个不同的界面

| | 界面A：顶部商人卡片条 | 界面B：商人选择列表 |
|---|---|---|
| 路径 | `Trader Screens Group / TopPanel / Container / TraderCards` | `Merchants List / Traders Container` |
| 原版问题 | 商人多时右侧卡片被玩家信息面板挡住 | 一行约 5 个，商人多时溢出屏幕 |
| 布局组件 | `HorizontalLayoutGroup`（单行、间距 5、卡片约 118×168） | `FlowLayoutGroup`（间距 10、卡片约 160×205） |
| 本插件做法 | 给 `TraderCards` 加 `ScrollRect`（横向、滚轮），内容宽度按实际卡片末端；**不改变父级/锚点/遮罩** | 保留 `FlowLayoutGroup`，按配置的行数换行；内容高度由插件精确控制后加纵向 `ScrollRect` |

> 注意：旧版 Kaeno 的 `anchorMin.x = 0.595 - 0.065*(count-9)` 公式在商人数量多时会把内容撑得过宽，
> 本项目不再使用该公式。

## 3. 界面A 滚动几何（v4.2.4 关键修复）

Unity `ScrollRect` 的滚动范围 = `content 尺寸 - viewport 尺寸`。原版容器 `Container` 是整条顶栏（约 1920 宽），
但卡片真正可见区域只到右侧玩家面板（`Right Person`）的左边缘（约 1262 宽），因此：

- 若 `viewport = Container`，可滚范围会少约 650px，滚到最右端时最后约 5~6 张卡停留在玩家面板后面；
- 旧 Kaeno 之所以“看起来正常”，是因为它用 anchor 公式把内容宽度撑得远大于实际卡片宽度，多余的滚动行程恰好补偿了这个差值（副作用是滚到底会多出一段空白）。

v4.2.4 的实现：

1. 创建一个隐藏的 `KaenoTraderCardsViewport`（`Container` 的子节点，只有 `RectTransform`，无 `Graphic`、无 `Mask`、无绘制）；
2. 它的左边缘 = `TraderCards` 原始左边缘（8），宽度 = `Right Person` 左边缘 - 左边缘；
3. `ScrollRect` 加在 `TraderCards` 上，`content = TraderCards`、`viewport = KaenoTraderCardsViewport`，**不改变 `TraderCards` 的父级与锚点**；
4. 内容宽度 = 实际参与排版的（`activeInHierarchy`）卡片末端（布局公式值与实际 rect 末端取较大值），不再使用 `max(childCount, _spawnedCards.Count)` 计数公式；
5. 滚动位置 = 内容宽 - 可见宽；滚到最右端时最后一张卡正好停在可见右边界。

## 4. 配置变更热更新

`ModConfig` 在 `ConfigEntry.SettingChanged` 时触发 `LayoutSettingsChanged` / `ScrollSpeedChanged` 事件。
两个 controller 在 `OnEnable` 订阅、`OnDisable` 取消订阅。

配置变更会触发界面A 重新应用布局。v4.2.4 起，重新应用前会记录当前 `horizontalNormalizedPosition`，
应用后恢复，避免“改设置后卡条跳回开头”。`ComputeVisibleWidth()` 基于 `Container`/`Right Person` 的静止几何，
不随滚动位置变化，也不受 `Canvas.scaleFactor` 影响（统一换算到参考分辨率像素）。

## 5. 诊断输出

设置 `[Advanced] Diagnostic Dump = true` 后，或由 v4.2.3+ 的内置诊断（默认始终记录，可删除输出文件）：

- `BepInEx\LogOutput.log`：常规日志（`[Kaeno-AdaptiveTraderUI]` 前缀）；
- `BepInEx\Kaeno-AdaptiveTraderUI-diag.txt`：界面A 的快照与逻辑自检（`CHECK1`~`CHECK7`）。

诊断文件包含：Canvas 缩放、Container/TraderCards/Right Person 几何、卡片数量（childCount / active / `_spawnedCards`）、
头尾卡片坐标、`ScrollRect` 配置、可滚范围、以及“滚到最右端时最后一张卡停在哪 / 内容右侧是否有空白尾”的计算结论。

## 6. 失败安全原则

- 所有 patch 与 controller 的 public/入口方法都包在 `try/catch` 中；
- 找不到节点/布局组件时保持原版布局，只记一次 warning；
- 所有可选的视觉改动（玩家卡美化）均可通过配置关闭；
- 卸载插件时所有布局改动都会在下次显示界面时恢复（不写存档、不改游戏文件）。
