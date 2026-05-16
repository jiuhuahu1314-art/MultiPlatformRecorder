using System.Threading.Tasks;

#nullable enable
namespace BililiveRecorder.Core.Platform
{
    public interface ILivePlatform
    {
        string PlatformId { get; }
        string DisplayName { get; }
        bool RequiresCookie { get; }
        bool RequiresProxy { get; }

        Task<LiveRoomInfo> GetRoomInfoAsync(string roomId, string? cookie = null, string? proxy = null);
    }
}
