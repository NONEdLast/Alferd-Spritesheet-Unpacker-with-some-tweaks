[English](README.md) | **简体中文**
# Alferd Spritesheet Unpacker with some tweaks

### 注意：本项目基于 GitHub 上的 [`Alferd-Spritesheet-Unpacker`](https://github.com/ForkandBeard/Alferd-Spritesheet-Unpacker/)。所有原始代码的版权均归原作者 [`ForkandBeard`](https://github.com/ForkandBeard)所有。更多许可信息，请参阅 [`LICENSE`](.\LICENSE)。

---

## 我为什么做这个
ASU 是一个年代久远但实用的工具，我已经用了相当一段时间。我还挺喜欢这个小工具的。
最近我在拆一张大图做点Mugen人物的时候，它跑得奇慢无比，而且占满了我的CPU，导致我的电脑直接卡住。
所以我打算做一些修改，让它更快一点、更准确一点。

## 新增功能
我引入了 GPU 加速来让它更快、并且不会让电脑卡死，加入了一个本地化语言包以便用于其他语言，并加入了一种新的精灵提取算法来让它更准确。
我还添加了一个“细化拆分”功能，你可以自动拆分选择的大块精灵，而不需要手动拆分（可能不完全准确）。

---

#### 使用方法
和原来一样，双击 .sln 文件即可。
或者用 build.ps1 或 build.bat 自行构建，产物在[`ASU\bin\Release`](ASU\bin\Release)。
它默认使用英语，你可以在“设置 - 语言”中更改。
我目前只添加了简体中文的本地化语言包，并且有计划制作更多语言。

---

由于这些调整都是直接在原始代码上进行的，因此本项目继承了与 [`LICENSE`](.\LICENSE) 相同的许可证。
我还保留了原始的 [`Readme`](.\README(Original).md) 供使用者阅读。
