<div align="center">

<a href="README.md"><img src="https://img.shields.io/badge/English-3A3A3C?style=for-the-badge" alt="English"></a>
<a href="README.zh-CN.md"><img src="https://img.shields.io/badge/%E7%AE%80%E4%BD%93%E4%B8%AD%E6%96%87-0A84FF?style=for-the-badge" alt="简体中文"></a>

<img src="docs/icon.png" width="96" alt="BrimDeck 图标">

# BrimDeck - macOS 灵动岛

BrimDeck 的 macOS 版。屏幕顶部的刘海平时收着，移上去展开，集中看 AI 编程工具的额度和正在播放的音乐，也可以看这台 Mac 的占用。<br>
界面和数据模型按 [Windows 原版](https://github.com/MoFeng2223/BrimDeck) 制作，用量、主题和设置仍由同一套 `BrimDeck.Core` 负责。

[![许可证](https://img.shields.io/badge/许可证-Apache%202.0-E5A385?style=flat-square)](LICENSE)
![界面语言](https://img.shields.io/badge/界面-简体中文%20%7C%20English-B9A8FF?style=flat-square)

<p><a href="#功能">功能</a> · <a href="#安装">安装</a> · <a href="#这一版的范围">这一版的范围</a> · <a href="#从源码运行">从源码运行</a></p>

<img src="docs/mac/usage.png" width="100%" alt="展开后的用量页：Claude、Codex、Antigravity、Cursor">

</div>

## 功能

平时贴在屏幕顶部正中。默认是刘海，也可以改成胶囊或指示条。鼠标移上去展开，移开收起；面板有焦点时按 Esc 也会收起。菜单栏图标可以再次打开、进设置或退出。

<img src="docs/mac/notch.png" width="280" alt="收起的刘海：配额环、歌名和律动条">

- **用量。** 一列一个工具，显示套餐、配额和重置倒计时，底部是今日和近 7 日的 Token 与费用估算。Claude Code、Codex、Cursor、Antigravity、ZCode、Kimi Code、DeepSeek Harness 等沿用原版的读取方式。Cursor 读的是 `~/Library/Application Support/Cursor` 里的登录状态。
- **音乐。** 接入「音乐」和 Spotify：封面占位、歌名、歌词行、进度和播放控制。Linux 上如果装了 `playerctl`，也会跟着系统里正在播放的播放器，方便开发时查看这一页。
- **系统监控。** CPU、内存、网速，以及占用最高的几个应用。配色跟着面板主题。
- **主题。** 深色、浅色，以及原版带的 One Dark Pro、GitHub、Dracula、Night Owl、赛博朋克等主题。
- **设置。** 语言、登录时启动、收起形态、要显示的工具。手动填写的 API 密钥保存在登录钥匙串里，不写进明文文件。

<img src="docs/mac/music.png" width="72%" alt="音乐页">

<img src="docs/mac/monitor.png" width="86%" alt="系统监控页">

## 安装

需要 macOS 12 或更新版本。从 [Releases](https://github.com/mutsuki14/BrimDeckMac/releases/latest) 下载对应芯片的 DMG，打开后把 BrimDeck 拖进「应用程序」。第一次打开若被拦截，在「系统设置 → 隐私与安全性」里选择仍要打开。

在 Mac 上也可以自己打包，脚本会用 `hdiutil` 生成 DMG：

```bash
scripts/publish-mac.sh osx-arm64   # Apple 芯片
scripts/publish-mac.sh osx-x64     # Intel
```

登录时启动写的是 `~/Library/LaunchAgents/app.brimdeck.mac.plist`。菜单栏图标可以退出。

## 这一版的范围

Windows 原版的窗口、原生接口和安装包仍在仓库里，给对照和以后同步用。macOS 版用 Avalonia 画界面。

- 播放器目前是「音乐」和 Spotify。
- 手动填写的密钥放在登录钥匙串。
- 系统监控显示 CPU、内存、网速和占用最高的应用。macOS 不提供无需额外授权的 GPU 占用，所以这一项留空。
- 前台应用进入全屏时，按设置里的规则收起或变成指示条。
- 全局热键还没接上。

Claude 桌面版写在钥匙串里的令牌，这一版还没解密。`~/.claude` 里 Claude Code 的登录状态可以读。读不到时面板只显示实际状态，不会补一个数。

## 从源码运行

```bash
dotnet run --project src/BrimDeck.Mac
```

`--preview` 用示例数据打开界面，不访问账号。`--language zh-CN` 指定界面语言。

```bash
dotnet run --project tests/BrimDeck.MacTests
dotnet run --project tests/BrimDeck.Tests
```

## 许可证

[Apache License 2.0](LICENSE)。Avalonia 与 SQLite 的许可见 [ThirdPartyNotices.txt](src/BrimDeck.Mac/ThirdPartyNotices.txt)。

系统监控的数据模型和 Windows 原版一样，参考并改编自 Nongfsq 的 [ftop](https://github.com/Nongfsq/ftop)（MIT）。
