using System.Reflection;
using AsyncKeyedLock;
using Discord.Interactions;
using Serilog;
using Serilog.Events;

namespace DougaBot.Modules.TopLevel;

public sealed partial class TopLevel : InteractionModuleBase<SocketInteractionContext>
{
    private AsyncKeyedLocker<string> _asyncKeyedLocker;

    private enum OperationType
    {
        Trim,
        Speed
    }

    private struct TrimParams
    {
        public float StartTime { get; set; }
        public float EndTime { get; set; }
    }

    private struct SpeedParams
    {
        public double Speed { get; set; }
    }
    private string Key => $"{Context.User.Id.ToString()}";

    public TopLevel(AsyncKeyedLocker<string> asyncKeyedLocker)
    {
        _asyncKeyedLocker = asyncKeyedLocker;
    }

    private async Task QueueHandler(string url, OperationType operationType, object operationParams)
    {
        using (await _asyncKeyedLocker.LockAsync(Key).ConfigureAwait(false))
        {
            switch (operationType)
            {
                case OperationType.Trim:
                    {
                        var trimParams = (TrimParams)operationParams;
                        Log.Information("[{Source}] {Message}",
                            MethodBase.GetCurrentMethod()?.DeclaringType?.Name,
                            $"{Context.User.Username}#{Context.User.Discriminator} locked: {url} ({trimParams.StartTime} - {trimParams.EndTime})");
                        await TrimTask(url, trimParams.StartTime, trimParams.EndTime);
                        Log.Information("[{Source}] {Message}",
                            MethodBase.GetCurrentMethod()?.DeclaringType?.Name,
                            $"{Context.User.Username}#{Context.User.Discriminator} released: {url} ({trimParams.StartTime} - {trimParams.EndTime})");
                        break;
                    }
                case OperationType.Speed:
                    {
                        var speedParams = (SpeedParams)operationParams;
                        Log.Information("[{Source}] {Message}",
                            MethodBase.GetCurrentMethod()?.DeclaringType?.Name,
                            $"{Context.User.Username}#{Context.User.Discriminator} locked: {url} ({speedParams.Speed})");
                        await SpeedTask(url, speedParams.Speed);
                        Log.Write(LogEventLevel.Information,
                            "[{Source}] {Message}",
                            MethodBase.GetCurrentMethod()?.DeclaringType?.Name,
                            $"{Context.User.Username}#{Context.User.Discriminator} released: {url} ({speedParams.Speed})");
                        break;
                    }

            }
        }
    }
}