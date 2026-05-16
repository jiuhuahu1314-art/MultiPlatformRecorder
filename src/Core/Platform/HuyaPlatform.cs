using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

#nullable enable
namespace BililiveRecorder.Core.Platform.Platforms
{
    public class HuyaPlatform : PlatformBase
    {
        public override string PlatformId => "huya";
        public override string DisplayName => "虎牙";
        public override bool RequiresCookie => true;

        public override async Task<LiveRoomInfo> GetRoomInfoAsync(string roomId, string? cookie = null, string? proxy = null)
        {
            var result = new LiveRoomInfo();
            try
            {
                var client = GetClient(cookie, proxy);

                var match = Regex.Match(roomId, @"huya\.com/(\w+)");
                if (match.Success)
                    roomId = match.Groups[1].Value;

                var pageUrl = $"https://www.huya.com/{roomId}";
                var html = await client.GetStringAsync(pageUrl).ConfigureAwait(false);

                var scriptMatch = Regex.Match(html, @"<script> window\.HNF_GLOBAL_INIT = (.*?)</script>");
                if (!scriptMatch.Success) return result;

                var json = JObject.Parse(scriptMatch.Groups[1].Value);
                var roomInfo = json["roomInfo"]?["tLiveInfo"]?["tLiveRoomInfo"];
                if (roomInfo == null) return result;

                var liveStatus = roomInfo["eLiveStatus"]?.Value<int>() ?? 0;
                result.IsLive = liveStatus >= 2;
                result.AnchorName = roomInfo["sNick"]?.ToString() ?? "";
                result.Title = roomInfo["sRoomName"]?.ToString() ?? "";
                result.AreaName = roomInfo["sGameFullName"]?.ToString() ?? "";
                result.CoverUrl = roomInfo["sScreenshot"]?.ToString() ?? "";

                if (result.IsLive)
                {
                    var streamInfo = json["roomInfo"]?["tLiveInfo"]?["tLiveStreamInfo"];
                    if (streamInfo != null)
                    {
                        var flvUrl = streamInfo["sFlvUrl"]?.ToString() ?? "";
                        var flvAntiCode = streamInfo["sFlvAntiCode"]?.ToString() ?? "";
                        var flvUrlSuffix = streamInfo["sFlvUrlSuffix"]?.ToString() ?? "flv";
                        if (!string.IsNullOrEmpty(flvUrl) && !string.IsNullOrEmpty(flvAntiCode))
                        {
                            result.FlvUrl = flvUrl + "/" + roomId + "." + flvUrlSuffix + "?" + flvAntiCode;
                            result.StreamUrl = result.FlvUrl;
                        }

                        var hlsUrl = streamInfo["sHlsUrl"]?.ToString() ?? "";
                        var hlsAntiCode = streamInfo["sHlsAntiCode"]?.ToString() ?? "";
                        var hlsUrlSuffix = streamInfo["sHlsUrlSuffix"]?.ToString() ?? "m3u8";
                        if (!string.IsNullOrEmpty(hlsUrl))
                        {
                            result.HlsUrl = hlsUrl + "/" + roomId + "." + hlsUrlSuffix + "?" + hlsAntiCode;
                            if (string.IsNullOrEmpty(result.StreamUrl))
                                result.StreamUrl = result.HlsUrl;
                        }
                    }
                }
            }
            catch { }
            return result;
        }
    }
}
