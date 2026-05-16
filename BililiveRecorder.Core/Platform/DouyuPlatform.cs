using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

#nullable enable
namespace BililiveRecorder.Core.Platform.Platforms
{
    public class DouyuPlatform : PlatformBase
    {
        public override string PlatformId => "douyu";
        public override string DisplayName => "斗鱼";
        public override bool RequiresCookie => true;

        public override async Task<LiveRoomInfo> GetRoomInfoAsync(string roomId, string? cookie = null, string? proxy = null)
        {
            var result = new LiveRoomInfo();
            try
            {
                var client = GetClient(cookie, proxy);
                var match = Regex.Match(roomId, @"douyu\.com/(\w+)");
                if (match.Success)
                    roomId = match.Groups[1].Value;

                Debug.WriteLine($"[Douyu] 查询房间: {roomId}");

                // betard API 获取房间状态（不需要签名）
                var betardResp = await client.GetStringAsync($"https://www.douyu.com/betard/{roomId}").ConfigureAwait(false);
                var betardJson = JObject.Parse(betardResp);
                var room = betardJson["room"];
                if (room == null)
                {
                    Debug.WriteLine("[Douyu] betard 返回无room数据");
                    return result;
                }

                result.AnchorName = room["nickname"]?.ToString() ?? "";
                result.Title = room["room_name"]?.ToString()?.Replace("&nbsp;", "") ?? "";
                var rid = room["room_id"]?.ToString() ?? roomId;

                var showStatus = room["show_status"]?.Value<int>() ?? 0;
                var videoLoop = room["videoLoop"]?.Value<int>() ?? 0;
                result.IsLive = videoLoop == 0 && showStatus == 1;

                Debug.WriteLine($"[Douyu] 房间: {result.AnchorName}, 直播中: {result.IsLive}");

                if (result.IsLive)
                {
                    // 尝试 getH5Play 无签名
                    try
                    {
                        var form = new FormUrlEncodedContent(new Dictionary<string, string>
                        {
                            ["rid"] = rid, ["rate"] = "-1"
                        });
                        client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                        var h5Resp = await client.PostAsync($"https://www.douyu.com/lapi/live/getH5Play/{rid}", form).ConfigureAwait(false);
                        var h5Str = await h5Resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                        var h5Json = JObject.Parse(h5Str);
                        var code = h5Json["error"]?.Value<int>() ?? h5Json["code"]?.Value<int>() ?? 0;
                        Debug.WriteLine($"[Douyu] getH5Play code={code}");

                        if (code == 0)
                        {
                            var data = h5Json["data"];
                            if (data != null)
                            {
                                var u = data["hls_live"]?.ToString() ?? data["rtmp_live"]?.ToString();
                                if (!string.IsNullOrEmpty(u))
                                {
                                    result.StreamUrl = u;
                                    Debug.WriteLine($"[Douyu] 流地址: {u}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[Douyu] getH5Play 错误: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Douyu] 查询错误: {ex.Message}");
            }
            return result;
        }

        
    }
}
