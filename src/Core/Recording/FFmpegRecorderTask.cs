using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using BililiveRecorder.Core.Event;
using Serilog;

#nullable enable
namespace BililiveRecorder.Core.Recording
{
    internal class FFmpegRecorderTask : IRecordTask
    {
        private readonly IRoom room;
        private readonly ILogger logger;
        private readonly string streamUrl;
        private Process? process;
        private bool stopped;
        private readonly Guid sessionId = Guid.NewGuid();
        private string? outputPath;
        private Timer? statsTimer;
        private long lastBytes;
        private DateTime lastStatsTime;

        public FFmpegRecorderTask(IRoom room, ILogger logger, string streamUrl)
        {
            this.room = room ?? throw new ArgumentNullException(nameof(room));
            this.logger = logger.ForContext<FFmpegRecorderTask>();
            this.streamUrl = streamUrl ?? throw new ArgumentNullException(nameof(streamUrl));
        }

        public Guid SessionId => this.sessionId;
        public bool Running => this.process != null && !this.process.HasExited;

        public event EventHandler<IOStatsEventArgs>? IOStats;
        public event EventHandler<RecordingStatsEventArgs>? RecordingStats;
        public event EventHandler<RecordFileOpeningEventArgs>? RecordFileOpening;
        public event EventHandler<RecordFileClosedEventArgs>? RecordFileClosed;
        public event EventHandler? RecordSessionEnded;

        public void RequestStop()
        {
            this.stopped = true;
            this.statsTimer?.Dispose();
            try
            {
                if (this.process != null && !this.process.HasExited)
                {
                    if (!this.process.CloseMainWindow())
                        this.process.Kill();
                    this.process.WaitForExit(5000);
                }
            }
            catch { }
        }

        public void SplitOutput() { }

        public async Task StartAsync()
        {
            await Task.Yield();
            var workDir = this.room.RoomConfig.WorkDirectory ?? AppContext.BaseDirectory;
            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            var safeName = SanitizeFileName(this.room.Name);
            var safeTitle = SanitizeFileName(this.room.Title);
            var outputDir = Path.Combine(workDir, "downloads", this.room.RoomConfig.Platform ?? "other", safeName);
            Directory.CreateDirectory(outputDir);
            this.outputPath = Path.Combine(outputDir, $"{timestamp}-{safeTitle}.ts");

            RecordFileOpening?.Invoke(this, new RecordFileOpeningEventArgs(this.room) { FullPath = this.outputPath });

            var ffmpegPath = FindFFmpeg();
            if (string.IsNullOrEmpty(ffmpegPath))
            {
                this.logger.Error("找不到 ffmpeg，无法录制");
                return;
            }

            this.logger.Information("ffmpeg: {Ffmpeg} {Url} -> {Path}", ffmpegPath, this.streamUrl, this.outputPath);

            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = $"-reconnect 1 -reconnect_at_eof 1 -reconnect_streamed 1 -reconnect_delay_max 30 -timeout 10000000 -i \"{this.streamUrl}\" -c copy -f mpegts \"{this.outputPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };

            this.process = new Process { StartInfo = startInfo };
            this.process.Start();
            this.lastStatsTime = DateTime.UtcNow;
            this.lastBytes = 0;

            this.statsTimer = new Timer(this.ReportStats, null, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2));

            _ = Task.Run(() =>
            {
                try
                {
                    while (!this.process.StandardError.EndOfStream)
                    {
                        var line = this.process.StandardError.ReadLine();
                        if (!string.IsNullOrEmpty(line))
                            this.logger.Verbose("ffmpeg: {Line}", line);
                    }
                }
                catch { }
            });

            await Task.Run(() => this.process.WaitForExit()).ConfigureAwait(false);
            this.statsTimer?.Dispose();

            if (this.process.ExitCode == 0 || this.stopped)
                this.logger.Information("录制完成: {Path}", this.outputPath);
            else
                this.logger.Warning("ffmpeg 退出码: {Code}", this.process.ExitCode);

            RecordFileClosed?.Invoke(this, new RecordFileClosedEventArgs(this.room) { FullPath = this.outputPath });
            RecordSessionEnded?.Invoke(this, EventArgs.Empty);
        }

        private void ReportStats(object? state)
        {
            try
            {
                if (this.process == null || this.process.HasExited || this.outputPath == null) return;

                var now = DateTime.UtcNow;
                var elapsed = now - this.lastStatsTime;

                if (!File.Exists(this.outputPath)) return;

                var currentBytes = new FileInfo(this.outputPath).Length;
                var deltaBytes = currentBytes - this.lastBytes;
                var deltaSecs = Math.Max(elapsed.TotalSeconds, 0.5);
                var mbps = (deltaBytes * 8.0) / (1024.0 * 1024.0) / deltaSecs;

                this.lastBytes = currentBytes;
                this.lastStatsTime = now;

                IOStats?.Invoke(this, new IOStatsEventArgs
                {
                    StreamHost = this.streamUrl,
                    EndTime = DateTimeOffset.UtcNow,
                    NetworkBytesDownloaded = (int)Math.Min(currentBytes, int.MaxValue),
                    NetworkMbps = mbps,
                    DiskBytesWritten = (int)Math.Min(currentBytes, int.MaxValue),
                    DiskMBps = mbps
                });

                RecordingStats?.Invoke(this, new RecordingStatsEventArgs
                {
                    TotalInputBytes = currentBytes,
                    TotalOutputBytes = currentBytes,
                    CurrentFileSize = currentBytes,
                    DurationRatio = Math.Min(deltaBytes / (1024.0 * 1024.0 / 8.0 * deltaSecs), 100.0),
                    PassedTime = deltaSecs,
                    AddedDuration = deltaSecs
                });
            }
            catch { }
        }

        private static string? FindFFmpeg()
        {
            var dlr = Path.Combine(AppContext.BaseDirectory, "..", "DouyinLiveRecorder_v4.0.7", "ffmpeg", "ffmpeg.exe");
            var candidates = new[]
            {
                dlr,
                Path.Combine(AppContext.BaseDirectory, "ffmpeg.exe"),
                Path.Combine(AppContext.BaseDirectory, "..", "ffmpeg.exe"),
                Path.Combine(AppContext.BaseDirectory, "..", "..", "ffmpeg.exe"),
                Path.Combine(AppContext.BaseDirectory, "lib", "miniffmpeg"),
                "ffmpeg.exe"
            };
            foreach (var c in candidates)
            {
                var full = Path.GetFullPath(c);
                if (File.Exists(full))
                    return full;
            }
            return null;
        }

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var sb = new System.Text.StringBuilder(name ?? "");
            foreach (var c in invalid)
                sb.Replace(c, '_');
            return sb.ToString();
        }
    }
}
