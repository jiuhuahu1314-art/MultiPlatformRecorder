using System.Collections.Generic;

#nullable enable
namespace BililiveRecorder.Core.Platform
{
    public class LiveRoomInfo
    {
        public bool IsLive { get; set; }
        public string AnchorName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string StreamUrl { get; set; } = string.Empty;
        public string HlsUrl { get; set; } = string.Empty;
        public string FlvUrl { get; set; } = string.Empty;
        public string CoverUrl { get; set; } = string.Empty;
        public string AreaName { get; set; } = string.Empty;
        public int OnlineCount { get; set; }
        public Dictionary<string, string>? StreamQualities { get; set; }
    }
}
