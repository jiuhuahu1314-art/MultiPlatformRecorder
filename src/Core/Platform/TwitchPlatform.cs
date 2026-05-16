using System;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

#nullable enable
namespace BililiveRecorder.Core.Platform.Platforms
{
    public class TwitchPlatform : PlatformBase
    {
        public override string PlatformId => "twitch";
        public override string DisplayName => "Twitch";
        public override bool RequiresCookie => true;
        public override bool RequiresProxy => true;

        private const string ClientId = "kimne78kx3ncx6brgo4mv6wki5h1ko";
        private string? loginName;

        public override async Task<LiveRoomInfo> GetRoomInfoAsync(string roomId, string? cookie = null, string? proxy = null)
        {
            var result = new LiveRoomInfo();
            try
            {
                var client = GetClient(cookie, proxy);
                client.DefaultRequestHeaders.TryAddWithoutValidation("Client-ID", ClientId);

                var match = Regex.Match(roomId, @"twitch\.tv/(\w+)");
                if (match.Success)
                    roomId = match.Groups[1].Value;
                loginName = roomId;

                var gqlQuery = new JObject
                {
                    ["query"] = @"
                        query($login: String!) {
                            user(login: $login) {
                                stream {
                                    title
                                    type
                                    viewersCount
                                }
                                displayName
                            }
                        }",
                    ["variables"] = new JObject { ["login"] = roomId }
                };

                var content = new StringContent(gqlQuery.ToString(), Encoding.UTF8, "application/json");
                var response = await client.PostAsync("https://gql.twitch.tv/gql", content).ConfigureAwait(false);
                var responseStr = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var json = JObject.Parse(responseStr);

                var user = json["data"]?["user"];
                if (user == null) return result;

                result.AnchorName = user["displayName"]?.ToString() ?? roomId;
                var stream = user["stream"];
                if (stream != null && stream["type"]?.ToString() == "live")
                {
                    result.IsLive = true;
                    result.Title = stream["title"]?.ToString() ?? "";
                    result.OnlineCount = stream["viewersCount"]?.Value<int>() ?? 0;

                    var streamUrl = await GetStreamUrlAsync(client, roomId).ConfigureAwait(false);
                    if (!string.IsNullOrEmpty(streamUrl))
                        result.StreamUrl = streamUrl;
                }
            }
            catch { }
            return result;
        }

        private async Task<string?> GetStreamUrlAsync(HttpClient client, string channelName)
        {
            try
            {
                var accessTokenQuery = new JObject
                {
                    ["query"] = @"
                        query($channelName: String!) {
                            streamPlaybackAccessToken(channelName: $channelName, params: {platform: web, playerType: site}) {
                                value
                                signature
                            }
                        }",
                    ["variables"] = new JObject { ["channelName"] = channelName }
                };

                var content = new StringContent(accessTokenQuery.ToString(), Encoding.UTF8, "application/json");
                var response = await client.PostAsync("https://gql.twitch.tv/gql", content).ConfigureAwait(false);
                var responseStr = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var json = JObject.Parse(responseStr);

                var token = json["data"]?["streamPlaybackAccessToken"];
                var sig = token?["signature"]?.ToString() ?? "";
                var value = token?["value"]?.ToString() ?? "";
                if (string.IsNullOrEmpty(value)) return null;

                var usherUrl = $"https://usher.ttvnw.net/api/channel/hls/{channelName}.m3u8?player=site&allow_source=true&allow_audio_only=true&sig={sig}&token={Uri.EscapeDataString(value)}";
                return usherUrl;
            }
            catch { }
            return null;
        }
    }
}
