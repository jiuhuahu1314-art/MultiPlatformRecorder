using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

#nullable enable
namespace BililiveRecorder.Core.Platform.Platforms
{
    public class YouTubePlatform : PlatformBase
    {
        public override string PlatformId => "youtube";
        public override string DisplayName => "YouTube";
        public override bool RequiresCookie => true;
        public override bool RequiresProxy => true;

        public override async Task<LiveRoomInfo> GetRoomInfoAsync(string roomId, string? cookie = null, string? proxy = null)
        {
            var result = new LiveRoomInfo();
            try
            {
                var client = GetClient(cookie, proxy);

                var match = Regex.Match(roomId, @"(?:youtube\.com/watch\?v=|youtu\.be/)([\w-]+)");
                if (match.Success)
                    roomId = match.Groups[1].Value;

                var pageUrl = $"https://www.youtube.com/watch?v={roomId}";
                var html = await client.GetStringAsync(pageUrl).ConfigureAwait(false);

                var ytInitialMatch = Regex.Match(html, @"ytInitialPlayerResponse\s*=\s*({.*?});");
                if (!ytInitialMatch.Success)
                    ytInitialMatch = Regex.Match(html, @"window\.__INITIAL_STATE__\s*=\s*({.*?});");

                if (!ytInitialMatch.Success) return result;

                var json = JObject.Parse(ytInitialMatch.Groups[1].Value);

                var videoDetails = json["videoDetails"] ?? json["microformat"]?["playerMicroformatRenderer"];
                if (videoDetails != null)
                {
                    result.AnchorName = videoDetails["author"]?.ToString()
                        ?? videoDetails["channelId"]?.ToString() ?? "";
                    result.Title = videoDetails["title"]?.ToString() ?? "";
                    var isLive = videoDetails["isLive"]?.Value<bool>() ?? false;
                    var isLiveContent = videoDetails["isLiveContent"]?.Value<bool>() ?? false;
                    result.IsLive = isLive || isLiveContent;
                    result.CoverUrl = videoDetails["thumbnail"]?["thumbnails"]?.Last?["url"]?.ToString() ?? "";
                }

                if (result.IsLive)
                {
                    var streamingData = json["streamingData"];
                    if (streamingData != null)
                    {
                        var hlsUrl = streamingData["hlsManifestUrl"]?.ToString();
                        if (!string.IsNullOrEmpty(hlsUrl))
                        {
                            result.HlsUrl = hlsUrl;
                            result.StreamUrl = hlsUrl;
                            return result;
                        }
                        var dashUrl = streamingData["dashManifestUrl"]?.ToString();
                        if (!string.IsNullOrEmpty(dashUrl))
                            result.StreamUrl = dashUrl;
                    }

                    var microformat = json["microformat"]?["playerMicroformatRenderer"];
                    if (microformat != null && string.IsNullOrEmpty(result.StreamUrl))
                    {
                        var broadcastUrl = microformat["url"]?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(broadcastUrl))
                            result.StreamUrl = broadcastUrl;
                    }
                }
            }
            catch { }
            return result;
        }
    }
}
