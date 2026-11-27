# 更新记录 / Changelog

## v1.1.0（2026-11-22）卡条记忆与匹配

- 新增：**界面A（顶部商人卡片条）位置记忆**——重新进入商人界面时（含从角色页按 ESC 回到交易界面、从商人选择列表再次进入），卡条停在你上次滚到的位置，不再跳回第一张卡。位置只记在本次游戏进程内存里，不写盘，退出游戏即清空。
- 新增：**选中商人自动匹配**——在商人选择列表里换成另一个商人后，卡条自动滚到该商人的卡片处；对齐方式可选 靠左 / 居中（默认）/ 靠右。选中商人没变时只恢复记忆位置，不主动跳转（不打断你正在看的地方）。
- 新增配置：`[Config] Remember Strip Position`（默认 `true`）、`[Config] Match Selected Trader`（默认 `true`）、`[Config] Selected Trader Alignment`（默认 `Center`）。两项开关都关 = 完全等价 v5.0.0 的表现。
- 修正：诊断 **CHECK4** 改为按 `Use Elastic` 推导期望的 `movementType`。旧版在默认配置（`Use Elastic = true`）下会把正常的 `Elastic` 误判为 FAIL，并提示"有外部代码在改它"；CHECK1/2/3/5/6/7 的判定与解释段落不变。
- 清理：删除 v4.2.2 主方案遗留的死代码（`EnsureViewport` / `ConfigureScrollForViewport` / `GetRightMargin`、`_viewportGo` 相关永假分支、`UILayoutTools` 4 个无调用点方法、`Plugin.Instance` 等），源码净减约 236 行；已用 IL 比对确认其余方法体没有任何指令被改写（纯删除）。
- 兼容性：配置路径与键名不变（`BepInEx\config\com.henryplaytime.adaptivetraderui.cfg`），旧配置可直接沿用；程序集 `1.1.0.0`。
- **版本号体系重置**：自本版起版本号改为 `1.x`，本版为 `1.1.0`。上一版是 `5.0.0`，`1.1.0` 是它的**功能超集**（新增卡条记忆与匹配），**不是降级**。

### 怎么用（本版新增的三个开关）

装好即生效，**默认全部开启，不需要做任何配置**。要改就在游戏内按 **F12**，或直接编辑
`BepInEx\config\com.henryplaytime.adaptivetraderui.cfg`：

| 配置项 | 默认 | 作用 |
|---|---|---|
| `[Config] Remember Strip Position` | `true` | 记住顶部商人卡条的位置：重新进入商人界面时停在你上次滚到的地方 |
| `[Config] Match Selected Trader` | `true` | 换成另一个商人后，卡条自动滚到该商人的卡片处 |
| `[Config] Selected Trader Alignment` | `Center` | 匹配时的对齐方式：`Start` 靠左 / `Center` 居中 / `End` 靠右（也认 `Left`/`Middle`/`Right`、`靠左`/`居中`/`靠右`） |

三个典型操作：

1. **列表里点第 70 个商人** → 进入交易界面后卡条直接停在他身上（默认居中），不用再滚。
2. **在卡条上滚到某处** → 按 ESC 回列表 → 再点**同一个**商人 → 卡条还在你刚才那个位置。
3. **列表里点另一个商人** → 卡条自动滚到新商人卡片；想让他贴左边就把对齐改成 `Start`。

想完全回到 v5.0.0 的老表现：`Remember Strip Position = false` + `Match Selected Trader = false`。

### 与原版 / 上一版对比

| 场景 | 原版 EFT | v5.0.0 | v1.1.0（本版） |
|---|---|---|---|
| 商人超过一屏 | 卡条看不到的部分**完全没有办法**（原版没有滚动） | 可鼠标滚轮横向滚动，但**每次进入都跳回第一张卡**，换完商人要重新滚过去 | 可滚动 + **记住上次位置**；换商人后**自动滚到他**，对齐可选 靠左/居中/靠右 |
| 装 80+ 商人时在卡条上换人 | 做不到，只能 ESC 回列表重新点 | 能做到，但每次进界面都要从头滚一遍 | 进去就在正确位置，滚动量接近 0 |
| 从列表选另一个商人再进入 | 原版没有卡条滚动，无此问题 | 卡条停在第一张，新选中的商人可能不在视野里 | 卡条直接对齐到新商人 |
| 配置项 | 无 | 滚轮速度 / 弹性 / 每行数量 / 诊断 | 以上 + 记忆开关 / 匹配开关 / 对齐方式 |

## v5.0.0（2026-11-15）Forge 发布准备版

- **Breaking / 身份变更**：为满足 Forge 的 GUID/命名规则，插件身份改为：
  - GUID：`com.henryplaytime.adaptivetraderui`（旧版为 `com.kaeno.TraderScrolling`）
  - BepInPlugin Mod Name：`henryplaytime-AdaptiveTraderUI`
  - 程序集 / DLL：`HPT-AdaptiveTraderUI.dll`（旧版为 `Kaeno-AdaptiveTraderUI.dll`）
  - 配置文件：`BepInEx\config\com.henryplaytime.adaptivetraderui.cfg`（旧版配置不会自动迁移）
  - 版本：`5.0.0`
- 升级提示：如果你之前安装过旧版 v4.2.4，请同时删除/改名旧的 `BepInEx\plugins\Kaeno-AdaptiveTraderUI.dll`，否则新旧两个 DLL 会同时加载并修改同一套商人 UI。
- 新增：启动时检测旧的 `Kaeno-TraderScrolling.dll`；若仍安装，则本次不启用任何 patch，并在日志中提示先删除旧插件，避免两个插件同时修改商人界面。
- 优化：诊断输出改为**默认关闭**，仅在 `[Advanced] Diagnostic Dump = true` 时写入 `BepInEx\HPT-AdaptiveTraderUI-diag.txt`；正常游玩不再产生诊断文件。
- 文档：新增 `docs/FORGE-SUBMISSION.md`（Forge 表单字段、测试清单、权限与 VirusTotal 要求）；README/构建脚本/示例配置同步更新。
- 功能与 v4.2.4 相同（界面A 滚动修复、界面B 可调行数、玩家卡美化、失败安全）。

## v4.2.4（2026-11-07）修复版

- 修复界面A（顶部商人卡片条）滑到最右端仍缺 1~2 个（实际约 5~6 个卡位）商人的问题。
  - 根因：`ScrollRect.viewport` 使用的是整条 `Container`（约 1920 宽），而卡片真正可见区只到 `Right Person` 左边缘（约 1262 宽），可滚范围少了约 650px，最右端时最后几张卡停在玩家信息面板后面。
  - 修复：新增隐藏的 `KaenoTraderCardsViewport`（`Container` 子节点，无 Graphic、无 Mask，不改变 TraderCards 的父级/锚点/绘制）作为滚动边界，宽度 = 卡片左边缘(8) → Right Person 左边缘。
- 修复界面A 滚到最右端会“拉出一大段空白”的问题。
  - 根因：内容宽度使用 `CountCards() = max(childCount, _spawnedCards.Count)` 乘公式计算，计数不一致或存在不可见子节点时内容比卡片实际末端宽出一截。
  - 修复：内容宽度改为按实际参与排版的（active）卡片末端计算；并禁用 TraderCards 上多余的 ScrollRect。
- 修复修改 `Use Elastic` 设置会触发界面A 重应用并“复现旧 bug”的问题。
  - 修复：配置变更重应用前记住滚动位置、应用后恢复；`Use Elastic` 对界面A 恢复真实语义（`true` = 弹性回弹，`false` = 硬性夹取；界面B 仍固定硬性夹取）。
- 修复 `ComputeUsableWidth()` 用已滚动后的世界坐标测量（滚动越远值越大）且世界像素/参考像素单位不一致的问题。
  - 修复：改为基于 Container / Right Person 的稳定测量，统一换算到参考分辨率像素。
- `PluginVersion` = 4.2.4，程序集 `4.2.4.0`。
- 仍保留诊断输出（`BepInEx\Kaeno-AdaptiveTraderUI-diag.txt`），用于验证修复后 `CHECK1/CHECK2` 是否为 PASS。

## v4.2.3-DIAG（2026-11-07）诊断版

- 不改变布局/滚动行为，仅增加界面A 诊断：在打开界面、配置变更重应用、滚到最右端、检测到外部改动时，把几何/计数/滚动范围与逻辑自检结论写入 `BepInEx\Kaeno-AdaptiveTraderUI-diag.txt`。
- 用于定位：viewport 宽度、内容宽度 vs 卡片末端、`childCount`/`active`/`_spawnedCards` 计数一致性、`movementType`、ScrollRect 数量、锚点是否被外部修改等。

## v4.2.2（2026-11-07）

- 修复界面A 在 v4.2.0/v4.2.1 中出现的“卡片整体上移/顶部被裁切、可被拖出一大段盖到交易界面区域”的问题。
  - 原因：界面A 之前使用“新建 wrapper viewport + RectMask2D”方案，改变了 TraderCards 的父级与锚点。
  - 修复：界面A 改回旧 Kaeno 的成熟方案 —— ScrollRect 直接加在 TraderCards 上、保持 prefab 原始锚点、仅按卡片数量设置内容宽度；滚动改为 `Clamped` 防回弹。

## v4.2.1（2026-11-07）

- 修复界面B（商人选择列表，`Wrap` 模式）纵向滚动“鼠标滚轮一拨就弹回、看不到下半部分”的问题。
  - 根因：`ContentSizeFitter(PreferredSize)` 会把内容高度改回旧值，`ScrollRect` 认为内容不高、可滚范围≈0，滚轮位移后又被 Elastic 回弹。
  - 修复：由插件精确控制内容高度（= FlowLayoutGroup 实际排版高度）、滚动改为 `Clamped`、viewport 居中并按可见区域限制尺寸。
- 商人列表滚动区域改为居中显示。

## v4.2.0（2026-11-07）

- 首个合并版：把 Kaeno-TraderScrolling（界面A 顶部商人卡条横向滚动 + 玩家卡美化）与
  AdjustableTraderRows（界面B 商人选择列表每行数量可调）合并为单个 BepInEx 插件。
- 新增 `[Layout]` 配置：`Merchants Layout Mode`（Wrap / Horizontal / None）、`Merchants Per Row`、`Trader Columns`（旧名兼容）。
- 新增 `[Advanced]` 配置：`Diagnostic Dump`、`Debug Logging`。
- 通过 `ModulePatch` 在 `TraderScreensGroup.Show` / `MerchantsList.Show` / `DisplayMoneyPanelTMPText.Show` 之后应用参数化布局。
