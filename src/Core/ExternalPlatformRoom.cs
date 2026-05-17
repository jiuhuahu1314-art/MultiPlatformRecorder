using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BililiveRecorder.Core.Config.V3;
using BililiveRecorder.Core.Event;
using BililiveRecorder.Core.Recording;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serilog;

#nullable enable
namespace BililiveRecorder.Core
{
    internal class ExternalPlatformRoom : IRoom
    {
        private readonly ILogger logger;
        private readonly CancellationTokenSource cts = new();
        private readonly string pythonPath;
        private readonly string bridgeScriptPath;
        private string? streamUrl;
        private IRecordTask? recordTask;
        private bool streaming;
        private bool autoRecordForThisSession = true;
        private bool disposedValue;
        private string platformName = "";
        private string _Name = string.Empty;
        private string _Title = string.Empty;

        public ExternalPlatformRoom(RoomConfig roomConfig, ILogger logger)
        {
            this.RoomConfig = roomConfig ?? throw new ArgumentNullException(nameof(roomConfig));
            this.logger = logger.ForContext<ExternalPlatformRoom>().ForContext("RoomId", roomConfig.RoomId);
            this.Stats = new RoomStats();
            this.platformName = GetPlatformDisplayName(roomConfig.Platform ?? "未知");
            this._Name = $"[{this.platformName}] 房间 {roomConfig.RoomId}";
            this._Title = "";
            this.logger.Information("创建外部平台房间: {Platform} Url={Url}", this.platformName, roomConfig.RoomUrl ?? "");

            this.pythonPath = FindPython();
            this.bridgeScriptPath = FindBridgeScript();

            if (string.IsNullOrEmpty(this.pythonPath) || !File.Exists(this.pythonPath))
                this.logger.Error("找不到 Python.exe ({Path})，无法获取流地址", this.pythonPath);
            if (string.IsNullOrEmpty(this.bridgeScriptPath) || !File.Exists(this.bridgeScriptPath))
                this.logger.Error("找不到 platform_bridge.py ({Path})，无法获取流地址", this.bridgeScriptPath);

            roomConfig.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(RoomConfig.AutoRecord)) return;
                if (roomConfig.AutoRecord)
                {
                    this.logger.Information("自动录制已开启");
                    this.autoRecordForThisSession = true;
                    if (this.streaming)
                        StartRecord();
                }
                else
                {
                    this.logger.Information("自动录制已关闭");
                    this.autoRecordForThisSession = false;
                    if (this.recordTask != null)
                        this.recordTask.RequestStop();
                }
            };

            _ = Task.Run(async () =>
            {
                await Task.Delay(1500);
                await this.CheckAndRecordLoopAsync();
            });
        }

        private static string FindPython()
        {
            var candidates = new[]
            {
                "E:\\keyan\\python.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Python", "python.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Python", "Python313", "python.exe"),
                "python.exe",
                "python3.exe",
            };
            foreach (var c in candidates)
            {
                try
                {
                    if (File.Exists(c)) return Path.GetFullPath(c);
                    using var p = Process.Start(new ProcessStartInfo(c, "--version") { UseShellExecute = false, CreateNoWindow = true });
                    if (p != null) { p.Close(); return c; }
                }
                catch { }
            }
            return "python.exe";
        }

        private static string FindBridgeScript()
        {
            var dir = AppContext.BaseDirectory;
            var candidates = new[]
            {
                Path.Combine(dir, "platform_bridge.py"),
                Path.Combine(dir, "..", "platform_bridge.py"),
                Path.Combine(dir, "..", "..", "platform_bridge.py"),
                "platform_bridge.py"
            };
            foreach (var c in candidates)
            {
                var full = Path.GetFullPath(c);
                if (File.Exists(full)) return full;
            }
            return Path.Combine(dir, "platform_bridge.py");
        }

        private async Task<BridgeResult?> CallBridgeAsync(string url)
        {
            try
            {
                if (!File.Exists(this.pythonPath))
                {
                    this.logger.Error("Python 不存在: {Path}", this.pythonPath);
                    return null;
                }
                if (!File.Exists(this.bridgeScriptPath))
                {
                    this.logger.Error("桥接脚本不存在: {Path}", this.bridgeScriptPath);
                    return null;
                }

                var scriptDir = Path.GetDirectoryName(this.bridgeScriptPath) ?? AppContext.BaseDirectory;
                var psi = new ProcessStartInfo(this.pythonPath, $"\"{this.bridgeScriptPath}\" \"{url}\"")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                    WorkingDirectory = scriptDir
                };

                using var process = Process.Start(psi);
                if (process == null)
                {
                    this.logger.Warning("无法启动进程: {Python}", this.pythonPath);
                    return null;
                }

                var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
                var error = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);

                if (!process.WaitForExit(30000))
                {
                    this.logger.Warning("桥接脚本超时 (30s)");
                    try { process.Kill(); } catch { }
                    return null;
                }

                if (!string.IsNullOrEmpty(error))
                    this.logger.Warning("桥接错误: {Error}", error);

                if (string.IsNullOrWhiteSpace(output))
                {
                    this.logger.Warning("桥接无输出");
                    return null;
                }

                output = output.Trim();
                var json = JObject.Parse(output);

                if (json["error"] != null)
                {
                    this.logger.Warning("桥接返回错误: {Error}", json["error"]!.ToString());
                    return null;
                }

                return json.ToObject<BridgeResult>();
            }
            catch (Exception ex)
            {
                this.logger.Warning(ex, "桥接调用失败: {Msg}", ex.Message);
                return null;
            }
        }

        private async Task CheckAndRecordLoopAsync()
        {
            var ct = this.cts.Token;
            this.logger.Information("开始轮询 {Platform} 直播状态", this.platformName);

            while (!ct.IsCancellationRequested)
            {
                try
                {
                    var query = this.RoomConfig.RoomUrl ?? this.RoomConfig.RoomId.ToString();
                    var result = await CallBridgeAsync(query).ConfigureAwait(false);

                    if (result != null)
                    {
                        if (!string.IsNullOrEmpty(result.anchor_name))
                        {
                            this.SetField(ref this._Name, $"[{this.platformName}] {result.anchor_name}", nameof(this.Name));
                        }
                        this.SetField(ref this._Title, result.title ?? "", nameof(this.Title));
                        this.SetField(ref this.streaming, result.is_live, nameof(this.Streaming));

                        if (result.is_live && !string.IsNullOrEmpty(result.stream_url))
                        {
                            this.streamUrl = result.stream_url;
                            this.logger.Information("检测到直播中: {Name} - {Title}", result.anchor_name, result.title);
                            if (this.autoRecordForThisSession && this.RoomConfig.AutoRecord)
                                this.StartRecord();
                        }
                        else if (result.is_live)
                        {
                            this.logger.Warning("直播中但无流地址: {Name}", result.anchor_name);
                        }
                        else
                        {
                            this.logger.Debug("未开播: {Name}", result.anchor_name ?? "无");
                        }
                    }
                    else
                    {
                        this.logger.Warning("桥接返回空结果");
                    }
                }
                catch (Exception ex)
                {
                    this.logger.Warning(ex, "查询出错: {Msg}", ex.Message);
                }

                try { await Task.Delay(30000, ct).ConfigureAwait(false); }
                catch (TaskCanceledException) { break; }
            }
        }

        public void StartRecord()
        {
            if (this.disposedValue || this.recordTask != null) return;

            if (string.IsNullOrEmpty(this.streamUrl))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        this.logger.Information("手动获取流地址...");
                        var query = this.RoomConfig.RoomUrl ?? this.RoomConfig.RoomId.ToString();
                        var result = await CallBridgeAsync(query).ConfigureAwait(false);
                        if (result?.is_live == true && !string.IsNullOrEmpty(result.stream_url))
                        {
                            this.streamUrl = result.stream_url;
                            this.SetField(ref this.streaming, true, nameof(this.Streaming));
                            if (!string.IsNullOrEmpty(result.anchor_name))
                        if (!string.IsNullOrEmpty(result.platform))
                            this.platformName = result.platform;
                        this.SetField(ref this._Name, $"[{result.platform ?? this.platformName}] {result.anchor_name}", nameof(this.Name));
                            this.StartRecord();
                        }
                        else
                        {
                            this.logger.Warning("无法获取流地址，请确认已开播");
                        }
                    }
                    catch (Exception ex)
                    {
                        this.logger.Warning(ex, "手动获取流地址失败");
                    }
                });
                return;
            }

            this.logger.Information("启动录制: {Url}", this.streamUrl);
            var task = new FFmpegRecorderTask(this, this.logger, this.streamUrl);
            task.RecordSessionEnded += this.OnRecordSessionEnded;
            task.RecordFileOpening += this.OnRecordFileOpening;
            task.RecordFileClosed += this.OnRecordFileClosed;
            task.IOStats += (_, e) =>
            {
                this.Stats.StreamHost = e.StreamHost;
                this.Stats.EndTime = e.EndTime;
                this.Stats.NetworkBytesDownloaded = e.NetworkBytesDownloaded;
                this.Stats.NetworkMbps = e.NetworkMbps;
                this.Stats.DiskBytesWritten = e.DiskBytesWritten;
                this.Stats.DiskMBps = e.DiskMBps;
                IOStats?.Invoke(this, e);
            };
            task.RecordingStats += (_, e) =>
            {
                this.Stats.SessionDuration = TimeSpan.FromMilliseconds(e.SessionDuration);
                this.Stats.TotalInputBytes = e.TotalInputBytes;
                this.Stats.TotalOutputBytes = e.TotalOutputBytes;
                this.Stats.CurrentFileSize = e.CurrentFileSize;
                this.Stats.DurationRatio = e.DurationRatio;
                this.Stats.PassedTime = e.PassedTime;
                RecordingStats?.Invoke(this, e);
            };
            this.recordTask = task;
            RecordSessionStarted?.Invoke(this, new RecordSessionStartedEventArgs(this) { SessionId = task.SessionId });
            this.OnPropertyChanged(nameof(this.Recording));

            _ = Task.Run(async () =>
            {
                try { await task.StartAsync().ConfigureAwait(false); }
                catch (Exception ex) { this.logger.Warning(ex, "录制出错"); }
            });
        }

        public void StopRecord() { this.autoRecordForThisSession = false; this.recordTask?.RequestStop(); }
        public void SplitOutput() { }
        public Task RefreshRoomInfoAsync() => Task.CompletedTask;
        public void MarkNextRecordShouldUseRawMode() { }

        private int recordRestartCount = 0;
        private DateTime recordStartTime = DateTime.MinValue;

        private void OnRecordSessionEnded(object? sender, EventArgs e)
        {
            this.recordTask = null;
            this.OnPropertyChanged(nameof(this.Recording));
            this.Stats.Reset();
            RecordSessionEnded?.Invoke(this, new RecordSessionEndedEventArgs(this) { SessionId = Guid.Empty });

            // Retry: if still streaming and not manually stopped, get fresh URL and restart
            if (this.streaming && this.autoRecordForThisSession && this.recordRestartCount < 5)
            {
                this.recordRestartCount++;
                this.logger.Information("录制结束，重试 #{Count}", this.recordRestartCount);
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await Task.Delay(3000);
                        var query = this.RoomConfig.RoomUrl ?? this.RoomConfig.RoomId.ToString();
                        var result = await CallBridgeAsync(query).ConfigureAwait(false);
                        if (result?.is_live == true && !string.IsNullOrEmpty(result.stream_url))
                        {
                            this.streamUrl = result.stream_url;
                            this.StartRecord();
                        }
                    }
                    catch { }
                });
            }
            else
            {
                this.recordRestartCount = 0;
            }
        }

        private void OnRecordFileOpening(object? sender, RecordFileOpeningEventArgs e) => RecordFileOpening?.Invoke(this, e);
        private void OnRecordFileClosed(object? sender, RecordFileClosedEventArgs e) => RecordFileClosed?.Invoke(this, e);

        #region IRoom
        public Guid ObjectId { get; } = Guid.NewGuid();
        public RoomConfig RoomConfig { get; }
        public int ShortId => 0;
        public string Name { get => this._Name; private set => this.SetField(ref this._Name, value, nameof(this.Name)); }
        public string PlatformDisplayName => this.platformName;
        public long Uid => 0;
        public string Title { get => this._Title; private set => this.SetField(ref this._Title, value, nameof(this.Title)); }
        public string AreaNameParent => string.Empty;
        public string AreaNameChild => string.Empty;
        public JObject? RawBilibiliApiJsonData => null;
        public bool Recording => this.recordTask != null;
        public bool Streaming { get => this.streaming; private set => this.SetField(ref this.streaming, value, nameof(this.Streaming)); }
        public bool DanmakuConnected => false;
        public bool AutoRecordForThisSession { get => this.autoRecordForThisSession; private set => this.SetField(ref this.autoRecordForThisSession, value, nameof(this.AutoRecordForThisSession)); }
        public RoomStats Stats { get; }
        public event EventHandler<RecordSessionStartedEventArgs>? RecordSessionStarted;
        public event EventHandler<RecordSessionEndedEventArgs>? RecordSessionEnded;
        public event EventHandler<RecordFileOpeningEventArgs>? RecordFileOpening;
        public event EventHandler<RecordFileClosedEventArgs>? RecordFileClosed;
        public event EventHandler<RecordingStatsEventArgs>? RecordingStats;
        public event EventHandler<IOStatsEventArgs>? IOStats;
        public event PropertyChangedEventHandler? PropertyChanged;
        #endregion

        protected void SetField<T>(ref T location, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(location, value)) return;
            location = value;
            if (propertyName != null)
                this.OnPropertyChanged(propertyName);
        }
        protected void OnPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        private static string GetPlatformDisplayName(string id)
        {
            return id switch
            {
                "douyu" => "斗鱼", "huya" => "虎牙", "douyin" => "抖音",
                "kuaishou" => "快手", "bilibili" => "B站", "yy" => "YY",
                "acfun" => "Acfun", "netease" => "网易CC", "bigo" => "Bigo",
                "baidu" => "百度", "weibo" => "微博", "soop" => "SOOP",
                "haixiu" => "嗨秀", "kugou" => "酷狗", "inke" => "映客",
                "zhihu" => "知乎", "showroom" => "ShowRoom", "picarto" => "Picarto",
                "17live" => "17Live", "twitcasting" => "TwitCasting",
                "tiktok" => "TikTok", "twitch" => "Twitch", "youtube" => "YouTube",
                _ => id
            };
        }

        public void Dispose()
        {
            if (!this.disposedValue)
            {
                this.disposedValue = true;
                this.cts.Cancel();
                this.cts.Dispose();
                this.recordTask?.RequestStop();
            }
        }

        private class BridgeResult
        {
            [JsonProperty("is_live")] public bool is_live { get; set; }
            [JsonProperty("anchor_name")] public string? anchor_name { get; set; }
            [JsonProperty("title")] public string? title { get; set; }
            [JsonProperty("stream_url")] public string? stream_url { get; set; }
            [JsonProperty("platform")] public string? platform { get; set; }
            [JsonProperty("error")] public string? error { get; set; }
        }
    }
}
