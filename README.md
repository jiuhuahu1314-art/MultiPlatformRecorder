# MultiPlatformRecorder

基于 [BililiveRecorder](https://github.com/BililiveRecorder/BililiveRecorder) 二次开发的多平台直播录制工具。

## 使用

1. 安装 Python 3.10+ 和 Node.js
2. `pip install streamget`
3. 双击 `BililiveRecorder.WPF\bin\Release\net472\BililiveRecorder.WPF.exe`
4. 输入直播间链接，自动识别平台，开播自动录制

## 鸣谢

| 项目 | 作者 | 协议 | 贡献 |
|---|---|---|---|
| [BililiveRecorder](https://github.com/BililiveRecorder/BililiveRecorder) | Genteure | GPL-3.0 | 基础框架、WPF UI、FLV 引擎 |
| [DouyinLiveRecorder](https://github.com/ihmily/DouyinLiveRecorder) | ihmily | MIT | 多平台录制方案 |
| [streamget](https://github.com/ihmily/streamget) | ihmily | MIT | 流地址获取库 |
| [FFmpeg](https://ffmpeg.org) | FFmpeg team | GPL | 录制引擎 |
