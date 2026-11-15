# 第三方许可与致谢 / Third-Party Notices

本项目的功能参考并合并了下面两个开源/公共领域项目的行为；本项目为**独立实现**（独立的代码结构、空引用保护、诊断与几何修复），
未直接复制上述项目的源码文件，但其中「玩家卡/金钱面板美化」的行为逻辑参考了 Kaeno-TraderScrolling 的 MIT 代码，
因此在本仓库、发行版与二进制中保留其版权与许可声明。

---

## 1. Kaeno-TraderScrolling

本项目的「界面A：顶部商人卡片条横向滚动」与「玩家卡美化」功能参考自该项目。

```
MIT License

Copyright (c) 2024 - 2025 Kaeno (previously Naekami) and CWX

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

## 2. AdjustableTraderRows

本项目的「界面B：商人选择列表可调每行数量 / 换行滚动」功能参考自该项目。

```
This is free and unencumbered software released into the public domain.

Anyone is free to copy, modify, publish, use, compile, sell, or
distribute this software, either in source code form or as a compiled
binary, for any purpose, commercial or non-commercial, and by any
means.

In jurisdictions that recognize copyright laws, the author or authors
of this software dedicate any and all copyright interest in the
software to the public domain. We make this dedication for the benefit
of the public at large and to the detriment of our heirs and
successors. We intend this dedication to be an overt act of
relinquishment in perpetuity of all present and future rights to this
software under copyright law.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
IN NO EVENT SHALL THE AUTHORS BE LIABLE FOR ANY CLAIM, DAMAGES OR
OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE,
ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
OTHER DEALINGS IN THE SOFTWARE.

For more information, please refer to <https://unlicense.org>
```

## 3. 游戏与平台

- 本项目是 **SPT (Single Player Tarkov)** 的客户端插件，不包含任何《Escape from Tarkov》、Battlestate Games 或 SPT 的
  游戏资源、程序集或商标文件。
- 「Escape from Tarkov」是 Battlestate Games 的商标；「SPT」是 SPT 项目。本项目与其作者、官方无任何隶属或背书关系。
- 使用本项目需要你自行合法拥有《Escape from Tarkov》并安装 SPT 与 BepInEx。

## 4. 构建依赖（不随仓库分发）

构建时引用你本地 SPT 安装目录中的以下程序集（均不提交到本仓库）：

- `BepInEx.dll` / `0Harmony.dll`
- `spt-reflection.dll`
- `Assembly-CSharp.dll`
- Unity 引擎程序集（`UnityEngine*.dll`）
- `Sirenix.Serialization.dll` / `Comfort.dll`
