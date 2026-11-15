---
name: Bug report
about: 反馈插件问题 / Report a bug
title: "[Bug] "
labels: bug
assignees: ""
---

## 环境 / Environment

- SPT 版本：<!-- 例如 SPT 4.1.6 -->
- EFT 版本：<!-- 例如 EFT 1.0.x -->
- 插件版本：<!-- 例如 Kaeno-AdaptiveTraderUI 4.2.4 -->
- BepInEx 版本：<!-- 例如 5.4.23.5 -->
- 是否已移除旧 `Kaeno-TraderScrolling.dll`：<!-- 是/否（必须为“是”） -->

## 问题描述 / Description

<!-- 清楚描述发生了什么，以及你期望发生什么 -->

## 复现步骤 / Steps to reproduce

1.
2.
3.

## 相关配置

```ini
# BepInEx\config\com.kaeno.TraderScrolling.cfg 中相关项
Scroll Wheel Speed = 30
Trader Strip Scrolling = true
Use Elastic = false
Merchants Layout Mode = Wrap
Merchants Per Row = 8
```

## 日志与诊断文件 / Logs

- 请把 `BepInEx\LogOutput.log` 附在这里（可打包成 zip）。
- 请把 `BepInEx\Kaeno-AdaptiveTraderUI-diag.txt` 附在这里（如果有）。
- 如果问题与界面A 滚动位置有关，请附带一张“滚到最右端”的截图，并说明哪些商人看不到。

> 提交前请确认没有把游戏文件、`Assembly-CSharp.dll` 或其它反编译产物放进附件。
