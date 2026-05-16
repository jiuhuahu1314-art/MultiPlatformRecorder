# MultiPlatformRecorder

基于 [BililiveRecorder](https://github.com/BililiveRecorder/BililiveRecorder) 的多平台直播录制工具。

## 使用

1. 安装 Python 3.10+，运行 `pip install streamget`
2. 双击 `src\App\bin\Release\net472\BililiveRecorder.WPF.exe` 或根目录快捷方式
3. 输入直播间链接，开播自动录制

## 构建

```bash
dotnet build src\App\MultiPlatformRecorder.csproj -c Release
```

## 鸣谢

| 项目 | 作者 | 协议 |
|---|---|---|
| [BililiveRecorder](https://github.com/BililiveRecorder/BililiveRecorder) | Genteure | GPL-3.0 |
| [DouyinLiveRecorder](https://github.com/ihmily/DouyinLiveRecorder) | ihmily | MIT |
| [streamget](https://github.com/ihmily/streamget) | ihmily | MIT |
