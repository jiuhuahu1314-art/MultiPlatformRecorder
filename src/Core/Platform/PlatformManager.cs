using System.Collections.Generic;

#nullable enable
namespace BililiveRecorder.Core.Platform
{
    public static class PlatformManager
    {
        private static readonly Dictionary<string, ILivePlatform> _platforms = new();

        public static void Register(ILivePlatform platform)
        {
            _platforms[platform.PlatformId] = platform;
        }

        public static ILivePlatform? Get(string platformId)
        {
            _platforms.TryGetValue(platformId, out var platform);
            return platform;
        }

        public static IEnumerable<ILivePlatform> GetAll() => _platforms.Values;

        public static bool IsPlatformSupported(string platformId) => _platforms.ContainsKey(platformId);

        public static void Init()
        {
            Register(new Platforms.DouyinPlatform());
            Register(new Platforms.DouyuPlatform());
            Register(new Platforms.HuyaPlatform());
            Register(new Platforms.KuaishouPlatform());
            Register(new Platforms.TwitchPlatform());
            Register(new Platforms.YouTubePlatform());
        }
    }
}
