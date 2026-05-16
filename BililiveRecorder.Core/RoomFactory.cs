using System;
using BililiveRecorder.Core.Config.V3;
using BililiveRecorder.Core.Platform;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace BililiveRecorder.Core
{
    internal class RoomFactory : IRoomFactory
    {
        private readonly IServiceProvider serviceProvider;

        public RoomFactory(IServiceProvider serviceProvider)
        {
            this.serviceProvider = serviceProvider;
        }

        public IRoom CreateRoom(RoomConfig roomConfig, int initDelayFactor)
        {
            var platform = roomConfig.Platform ?? "bilibili";
            if (platform == "bilibili" || !PlatformManager.IsPlatformSupported(platform))
            {
                var scope = this.serviceProvider.CreateScope();
                var sp = scope.ServiceProvider;
                return ActivatorUtilities.CreateInstance<Room>(sp, scope, roomConfig, initDelayFactor);
            }
            else
            {
                var logger = this.serviceProvider.GetRequiredService<ILogger>();
                return new ExternalPlatformRoom(roomConfig, logger);
            }
        }
    }
}
