using System;
using System.Net.Http;
using System.Threading.Tasks;

#nullable enable
namespace BililiveRecorder.Core.Platform
{
    public abstract class PlatformBase : ILivePlatform
    {
        protected static readonly HttpClient http = new HttpClient(new HttpClientHandler
        {
            AllowAutoRedirect = true,
            AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
        });

        public abstract string PlatformId { get; }
        public abstract string DisplayName { get; }
        public virtual bool RequiresCookie => false;
        public virtual bool RequiresProxy => false;

        public abstract Task<LiveRoomInfo> GetRoomInfoAsync(string roomId, string? cookie = null, string? proxy = null);

        protected void SetDefaultHeaders(string? cookie = null)
        {
            http.DefaultRequestHeaders.Clear();
            http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
            http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
            http.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "zh-CN,zh;q=0.9,en;q=0.8");
            if (!string.IsNullOrEmpty(cookie))
                http.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", cookie);
        }

        protected HttpClient GetClient(string? cookie = null, string? proxy = null)
        {
            if (!string.IsNullOrEmpty(proxy))
            {
                var handler = new HttpClientHandler
                {
                    Proxy = new System.Net.WebProxy(proxy),
                    AllowAutoRedirect = true,
                    AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
                };
                var client = new HttpClient(handler);
                ApplyHeaders(client, cookie);
                client.Timeout = TimeSpan.FromSeconds(15);
                return client;
            }

            ApplyHeaders(http, cookie);
            http.Timeout = TimeSpan.FromSeconds(15);
            return http;
        }

        private void ApplyHeaders(HttpClient client, string? cookie)
        {
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
            client.DefaultRequestHeaders.TryAddWithoutValidation("Accept-Language", "zh-CN,zh;q=0.9");
            if (!string.IsNullOrEmpty(cookie))
                client.DefaultRequestHeaders.TryAddWithoutValidation("Cookie", cookie);
        }
    }
}
