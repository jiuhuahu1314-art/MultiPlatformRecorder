using System;
using System.Diagnostics;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

#nullable enable
namespace BililiveRecorder.Core.Platform.Platforms
{
    public class KuaishouPlatform : PlatformBase
    {
        public override string PlatformId => "kuaishou";
        public override string DisplayName => "快手";
        public override bool RequiresCookie => true;

        public override async Task<LiveRoomInfo> GetRoomInfoAsync(string roomId, string? cookie = null, string? proxy = null)
        {
            var result = new LiveRoomInfo();
            try
            {
                var client = GetClient(cookie, proxy);

                var match = Regex.Match(roomId, @"kuaishou\.com/u/(\w+)");
                if (!match.Success)
                    match = Regex.Match(roomId, @"kuaishou\.com/(\w+)");
                if (match.Success)
                    roomId = match.Groups[1].Value;

                Debug.WriteLine($"[快手] 查询: {roomId}");

                var apiUrl = $"https://live.kuaishou.com/u/{roomId}";
                var html = await client.GetStringAsync(apiUrl).ConfigureAwait(false);

                var scriptMatch = Regex.Match(html, @"window\.__INITIAL_STATE__\s*=\s*({.*?});");
                if (!scriptMatch.Success)
                {
                    scriptMatch = Regex.Match(html, @"<script[^>]*>window\.__INITIAL_STATE__\s*=\s*({.*?})</script>");
                }
                if (!scriptMatch.Success)
                {
                    Debug.WriteLine("[快手] 未找到 INITIAL_STATE");
                    return result;
                }

                var json = JObject.Parse(scriptMatch.Groups[1].Value);
                var liveInfo = json["liveroom"]?["liveInfo"] ?? json["liveInfo"] ?? json["liveroom"];
                if (liveInfo == null)
                {
                    Debug.WriteLine("[快手] 未找到 liveInfo");
                    return result;
                }

                var liveStatus = liveInfo["liveStreamStatus"]?.Value<int>() ?? 0;
                result.IsLive = liveStatus == 1 || liveStatus == 2;
                result.AnchorName = liveInfo["user"]?["name"]?.ToString()
                    ?? liveInfo["nick"]?.ToString() ?? liveInfo["userName"]?.ToString() ?? "";
                result.Title = liveInfo["title"]?.ToString() ?? "";
                result.CoverUrl = liveInfo["coverUrl"]?.ToString() ?? "";

                Debug.WriteLine($"[快手] {result.AnchorName} 直播中: {result.IsLive}");

                if (result.IsLive)
                {
                    var streamUrls = liveInfo["multiStreams"] as JArray;
                    if (streamUrls != null)
                    {
                        foreach (var stream in streamUrls)
                        {
                            var url = stream["src"]?.ToString() ?? stream["url"]?.ToString() ?? stream["adaptationSet"]?["representation"]?[0]?["url"]?.ToString() ?? "";
                            if (!string.IsNullOrEmpty(url) && string.IsNullOrEmpty(result.StreamUrl))
                            {
                                result.StreamUrl = url;
                                if (url.Contains(".m3u8"))
                                    result.HlsUrl = url;
                                else
                                    result.FlvUrl = url;
                                Debug.WriteLine($"[快手] 流地址: {url}");
                            }
                        }
                    }

                    if (string.IsNullOrEmpty(result.StreamUrl))
                    {
                        var playbackUrl = liveInfo["playbackUrl"]?.ToString()
                            ?? liveInfo["streamUrl"]?.ToString()
                            ?? liveInfo["liveStream"]?["url"]?.ToString()
                            ?? liveInfo["liveUrl"]?.ToString();
                        if (!string.IsNullOrEmpty(playbackUrl))
                        {
                            result.StreamUrl = playbackUrl;
                            Debug.WriteLine($"[快手] 流地址(备选): {playbackUrl}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[快手] 错误: {ex.Message}");
            }
            return result;
        }
    }
}
