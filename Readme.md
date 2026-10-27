**English** | [简体中文](README.zh.md)
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
