# MultiPlatformRecorder

基于 [BililiveRecorder](https://github.com/BililiveRecorder/BililiveRecorder) 二次开发的多平台直播录制工具。

## 初衷

喜爱 BililiveRecorder 的 WPF 界面和录制体验，希望能在保留原有 B 站录制能力的基础上，支持更多直播平台。

本项目仅为技术分享和个人使用，**完全无恶意**。所有原作者的版权和协议均被尊重。

## 使用

1. 安装 Python 3.10+，运行 `pip install streamget`
2. 编译后双击根目录的 **`启动.bat`**
3. 输入直播间链接，开播自动录制

编译产物也在 `src\App\bin\Release\net472\BililiveRecorder.WPF.exe`
## 构建

```bash
dotnet build src\App\MultiPlatformRecorder.csproj -c Release
```


## 平台支持

| 平台 | 状态 | 备注 |
|---|---|---|
| B站 | ✅ | 弹幕/修复/metadata 完整支持 |
| 斗鱼/虎牙/抖音/YY | ✅ | |
| Twitch/YouTube | ⚠️ | 需要海外代理 |
还有原项目的其他网站，但没一一测试

## 鸣谢

| 项目 | 作者 | 协议 | 贡献 |
|---|---|---|---|
| [BililiveRecorder](https://github.com/BililiveRecorder/BililiveRecorder) | [Genteure](https://github.com/BililiveRecorder) | GPL-3.0 | 基础框架、WPF UI、FLV 引擎、弹幕录制 |
| [DouyinLiveRecorder](https://github.com/ihmily/DouyinLiveRecorder) | [ihmily](https://github.com/ihmily) | MIT | 多平台直播录制方案 |
| [streamget](https://github.com/ihmily/streamget) | [ihmily](https://github.com/ihmily) | MIT | 多平台流地址获取库 |
| [FFmpeg](https://ffmpeg.org) | FFmpeg team | GPL | 视频录制引擎 |

## 安全声明

- 本工具不会收集任何个人信息
- 所有请求直接发往各直播平台，不经过任何第三方服务器
- 不包含任何遥测、统计或广告
- 源代码完全公开，可自行审查

## 协议

本项目基于 GPL-3.0 协议开源。详见 [LICENSE](LICENSE)。
