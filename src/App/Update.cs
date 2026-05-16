using System;
using System.Threading.Tasks;
using Serilog;
using Squirrel;

#nullable enable
namespace BililiveRecorder.WPF
{
    internal class Update
    {
        private readonly ILogger logger;

        private Task updateInProgress = Task.CompletedTask;

        public Update(ILogger logger)
        {
            this.logger = logger.ForContext<Update>();
        }

        public async Task UpdateAsync()
        {
            if (!this.updateInProgress.IsCompleted)
                await this.updateInProgress;
            this.updateInProgress = this.RealUpdateAsync();
            await this.updateInProgress;
        }

        public async Task WaitForUpdatesOnShutdownAsync() => await this.updateInProgress.ContinueWith(ex => { }, TaskScheduler.Default).ConfigureAwait(false);

        private async Task RealUpdateAsync()
        {
            this.logger.Information("此版本不检查自动更新。");
            await Task.CompletedTask;
        }
    }
}
