# The Forge 发布前检查清单 / Forge Submission Notes

> **重要：在完成“原作者许可”之前，不要提交到 The Forge。**
> Forge 的 [Content Guidelines](https://sp-mod.com/content-guidelines) 明确要求：使用/修改他人作品必须取得原作者**明确许可**，
> 仅在描述里署名不能代替许可。即使上游是 MIT/公有领域代码，Forge 平台规则仍然更严格。

## 1. 当前身份

| 项 | 值 |
|---|---|
| GUID | `com.henryplaytime.adaptivetraderui` |
| BepInPlugin Mod Name | `henryplaytime-AdaptiveTraderUI` |
| 版本 | `5.0.0` |
| 程序集 | `HPT-AdaptiveTraderUI.dll` |
| 配置文件 | `BepInEx\config\com.henryplaytime.adaptivetraderui.cfg` |
| 源码 | https://github.com/henryplaytime/HPT-AdaptiveTraderUI |

Forge 规则核对：

- GUID 必须与 `[BepInPlugin]` 和 Forge 表单一致：本仓库已改为独立 GUID，**不再与 Forge 上已有的
  `Kaeno-TraderScrolling`（`com.kaeno.traderscrolling`）冲突**。
- Mod Name 必须为“用户名-Mod Name”：已改为 `henryplaytime-AdaptiveTraderUI`。
- 版本必须在 csproj、`[BepInPlugin]`、发布包、Forge 版本页中全部为 `5.0.0`。

## 2. 许可状态（提交前必须补齐）

| 上游 | 作者/页面 | 许可 | 需要做什么 |
|---|---|---|---|
| Kaeno-TraderScrolling | [CWX](https://sp-mod.com/user/9668/cwx)（owner）、[Kaeno](https://sp-mod.com/user/26188/kaeno)、[DrakiaXYZ](https://sp-mod.com/user/27605/drakiaxyz)；[mod 1089](https://sp-mod.com/mod/1089/kaeno-traderscrolling) | MIT | **联系 CWX 取得明确许可**，并确认是否需要 Kaeno/DrakiaXYZ 同意；保留书面/DM 截图 |
| Adjustable Trader Rows | [JustNU](https://sp-mod.com/user/143/justnu)；[mod 1022](https://sp-mod.com/mod/1022/adjustable-trader-rows) | Unlicense / public domain | 原页面已写明“任何人可更新/接管开发”；保存截图/链接作为依据 |
| Adjustable Trader Rows REVIVED（SPT 4.x） | [Wrath_Jr](https://sp-mod.com/user/71782/wrath-jr)；[mod 2723](https://sp-mod.com/mod/2723/adjustable-trader-rows-revived) | 页面未单独声明，沿用原 mod 的 public domain 声明 | 礼貌知会并署名；如对方有异议再调整 |

Forge 规则允许对合并/衍生内容进行审核；如果 staff 对“合并两个 mod 是否算 mod compilation”有疑问，
可先邮件 **clodan-spt@hotmail.com** 说明：这是独立实现的单一插件（不是打包合集），已取得/正在取得作者许可，
并附上源码与许可证据。

## 3. Forge 表单字段参考

| 字段 | 建议值 |
|---|---|
| Name | `Adaptive Trader UI`（≤75） |
| Mod GUID | `com.henryplaytime.adaptivetraderui` |
| Teaser | `One plugin for trader UI: horizontal trader-strip scrolling, configurable merchant rows, and a cleaner player card for SPT 4.1.6.` |
| License | `MIT` |
| Category | `Traders`（也可选 `UI`） |
| Additional Authors | 取得同意后再添加 CWX / Kaeno / JustNU / Wrath_Jr；否则先留空，在描述 Credits 区署名 |
| Source Code Links | ① `https://github.com/henryplaytime/HPT-AdaptiveTraderUI`（Label: `Source code`）② `https://github.com/henryplaytime/HPT-AdaptiveTraderUI/releases`（Label: `Releases`，可选） |
| Publish Date | 完成许可、测试、VirusTotal 前**留空**（留空=不发布） |
| Contains Ads | 否 |
| Show Cheat Warning | 否（UI/QoL，非 ESP/自瞄类，仅供 SPT 单机） |
| Comments | 启用 |
| Issues | 启用 |
| Comment Version Tags | 默认（Major=Red / Minor=Amber / Patch=Amber） |
| Add-ons | 建议启用“Disable Add-ons”（本插件不提供 add-on 支持） |
| Mod Lists | 启用 |
| Notifications | 按个人偏好 |

### 版本（Add Version）字段

| 字段 | 建议值 |
|---|---|
| Version | `5.0.0` |
| SPT version constraint | `~4.1.6`（仅在 4.1.6 实测；不要宣称未测试的版本） |
| Dependencies | 无（BepInEx 是运行环境，不是 Forge mod 依赖） |
| File | `HPT-AdaptiveTraderUI-v5.0.0.zip`（zip/7z，无密码，解压即用） |
| Changelog | 复制 `CHANGELOG.md` 的 v5.0.0 段 |
| VirusTotal | 该版本 zip/dll 的公开扫描链接（每个版本更新） |
| Fika compatibility | `Unknown`（未测试不要声称兼容） |

## 4. 发布前必须完成

- [ ] 取得 CWX（及 Kaeno/DrakiaXYZ 如需要）的明确许可；保存证据。
- [ ] 确认 JustNU 的 public domain 声明链接/截图已保存；知会 Wrath_Jr。
- [ ] 在**全新 SPT 4.1.6 + BepInEx 5.4.23.5** 环境实测：
  - [ ] 商人界面顶部卡条滚到最后一个商人（30+ 商人场景）
  - [ ] 商人选择列表 Wrap 8/11、纵向滚动、点击切换商人、Trade/Tasks/Services 正常
  - [ ] F12 改设置后卡条不跳回开头；`LogOutput.log` 无 error
  - [ ] 删除/改名旧 `Kaeno-TraderScrolling.dll` 后本插件正常工作；旧插件仍存在时会提示且不启用 patch
- [ ] 用新 zip + DLL 做 VirusTotal 扫描，保存公开链接。
- [ ] 更新 GitHub Release v5.0.0，确认源码/构建与上传文件一致。
- [ ] 再填写并提交 Forge。

## 5. 相关链接

- [Content Guidelines](https://sp-mod.com/content-guidelines)
- [Community Standards](https://sp-mod.com/community-standards)
- [Kaeno-TraderScrolling](https://sp-mod.com/mod/1089/kaeno-traderscrolling)
- [Adjustable Trader Rows](https://sp-mod.com/mod/1022/adjustable-trader-rows)
- [Adjustable Trader Rows REVIVED](https://sp-mod.com/mod/2723/adjustable-trader-rows-revived)
