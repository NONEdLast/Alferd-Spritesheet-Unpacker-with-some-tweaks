<a id="English"></a>
**English** | [简体中文](#简体中文)
# Alferd Spritesheet Unpacker with some tweaks

### NOTICE: This project is based on [`Alferd-Spritesheet-Unpacker`](https://github.com/ForkandBeard/Alferd-Spritesheet-Unpacker/) on GitHub. Copyrights of all original codes belongs to original author [`ForkandBeard`](https://github.com/ForkandBeard). For more licensing information, please refer to [`LICENSE`](.\LICENSE).

---

## Why did I make this
ASU is an old but useful tool, I have used it for a really long time. I like this little tool.
Recently when I was unpacking some big spritesheets to get some sprites for my Mugen stuff, it works **SO** slow and it eats up almost all my CPU, causing my PC to stuck for a long time.
So I decide to make some changes, to make it a bit faster and more accurate.

## What's new
I introduced GPU acceleration to make it faster, a localization pack so it can be used in other languages, and a new image-cropping algorithm to make it more accurate
There's also a "Refine Split" so you can do some auto-split instead of splitting them manually(Might not be accurate).

---

#### To use
Same as original, just double click the .sln file.
Or build it yourself with build.ps1 or build.bat, then fetch the output in [`ASU\bin\Release`](ASU\bin\Release).
It uses English as its defalut language, and you can change that in Settings - Language.
I only added a localization pack for Simplified Chinese for now and is planning to make more.

---

As the tweaks are all made directly from the original code, this project has inherited the same license as [`LICENSE`](.\LICENSE).
I also kept the original [`Readme`](.\README(Original).md) so you can read it.

---

<a id="简体中文"></a>
[English](#English) | **简体中文**
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
