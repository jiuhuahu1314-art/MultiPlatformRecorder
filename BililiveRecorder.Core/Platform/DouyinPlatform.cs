using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Jint;
using Newtonsoft.Json.Linq;

#nullable enable
namespace BililiveRecorder.Core.Platform.Platforms
{
    public class DouyinPlatform : PlatformBase
    {
        public override string PlatformId => "douyin";
        public override string DisplayName => "抖音";
        public override bool RequiresCookie => true;

        public override async Task<LiveRoomInfo> GetRoomInfoAsync(string roomId, string? cookie = null, string? proxy = null)
        {
            var result = new LiveRoomInfo();
            try
            {
                var client = GetClient(cookie, proxy);
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
                client.DefaultRequestHeaders.TryAddWithoutValidation("Referer", "https://live.douyin.com/");

                var webRid = ExtractRoomId(roomId);
                if (string.IsNullOrEmpty(webRid)) return result;

                var params_ = new Dictionary<string, string>
                {
                    ["aid"] = "6383",
                    ["app_name"] = "douyin_web",
                    ["live_id"] = "1",
                    ["device_platform"] = "web",
                    ["language"] = "zh-CN",
                    ["browser_language"] = "zh-CN",
                    ["browser_platform"] = "Win32",
                    ["browser_name"] = "Chrome",
                    ["browser_version"] = "120.0.0.0",
                    ["web_rid"] = webRid,
                    ["msToken"] = "",
                };

                var queryString = BuildQueryString(params_);
                var apiUrl = "https://live.douyin.com/webcast/room/web/enter/?" + queryString;

                try
                {
                    var response = await client.GetAsync(apiUrl).ConfigureAwait(false);
                    var responseStr = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var json = JObject.Parse(responseStr);

                    var data = json["data"];
                    if (data == null || !data.HasValues) return result;

                    var roomData = data["data"]?[0];
                    var userData = data["user"];
                    if (roomData == null) return result;

                    result.AnchorName = userData?["nickname"]?.ToString() ?? "";

                    var status = roomData["status"]?.Value<int>() ?? 0;
                    result.IsLive = status == 2;

                    var room = roomData["room"] ?? roomData;
                    if (room != null)
                    {
                        result.Title = room["title"]?.ToString() ?? "";
                        result.CoverUrl = room["cover"]?["url_list"]?.Last?.ToString()
                            ?? room["cover"]?["url_list"]?[0]?.ToString() ?? "";
                    }

                    if (result.IsLive)
                    {
                        var streamUrl = roomData["stream_url"];
                        if (streamUrl != null)
                        {
                            var flvPullUrl = streamUrl["flv_pull_url"];
                            if (flvPullUrl != null)
                            {
                                var qualities = new Dictionary<string, string>();
                                foreach (var prop in flvPullUrl.Children<JProperty>())
                                {
                                    var url = prop.Value?.ToString() ?? "";
                                    qualities[prop.Name] = url;
                                    if (string.IsNullOrEmpty(result.StreamUrl))
                                        result.StreamUrl = url;
                                }
                                result.StreamQualities = qualities;
                                result.FlvUrl = result.StreamUrl;
                            }

                            var hlsPullUrlMap = streamUrl["hls_pull_url_map"];
                            if (hlsPullUrlMap != null)
                            {
                                foreach (var prop in hlsPullUrlMap.Children<JProperty>())
                                {
                                    var url = prop.Value?.ToString() ?? "";
                                    if (!string.IsNullOrEmpty(url))
                                    {
                                        result.HlsUrl = url;
                                        if (string.IsNullOrEmpty(result.StreamUrl))
                                            result.StreamUrl = url;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Douyin API error: {ex.Message}");
                }
            }
            catch { }
            return result;
        }

        private static string? ExtractRoomId(string input)
        {
            var match = Regex.Match(input, @"live\.douyin\.com/(\d+)");
            if (match.Success) return match.Groups[1].Value;

            match = Regex.Match(input, @"v\.douyin\.com/(\w+)");
            return match.Success ? match.Groups[1].Value : input;
        }

        private static string BuildQueryString(Dictionary<string, string> params_)
        {
            var sb = new StringBuilder();
            foreach (var kv in params_)
            {
                if (sb.Length > 0) sb.Append('&');
                sb.Append(Uri.EscapeDataString(kv.Key));
                sb.Append('=');
                sb.Append(Uri.EscapeDataString(kv.Value));
            }
            return sb.ToString();
        }
    }
}
