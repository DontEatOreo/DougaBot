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
        public string StartTime { get; set; }
        public string EndTime { get; set; }
    }

    private struct SpeedParams
    {
        public double Speed { get; set; }
    }
    private string Key => Context.User.Id.ToString();

    public TopLevel(AsyncKeyedLocker<string> asyncKeyedLocker)
    {
        _asyncKeyedLocker = asyncKeyedLocker;
    }

    private async Task QueueHandler(string url, OperationType operationType, object operationParams)
    {
        var remainingCount = _asyncKeyedLocker.GetRemainingCount(Key);
        if (remainingCount > 0)
            await FollowupAsync(
                $"Your video is in position {_asyncKeyedLocker.GetRemainingCount(Key)} in the queue.",
                ephemeral: true,
                options: GlobalTasks.Options);

        using var loc = await _asyncKeyedLocker.LockAsync(Key).ConfigureAwait(false);

        switch (operationType)
        {
            case OperationType.Trim:
                {
                    var trimParams = (TrimParams)operationParams;
                    var trimLockMsg =
                        $"{Context.User.Username}#{Context.User.Discriminator} locked: {url} ({trimParams.StartTime} - {trimParams.EndTime})";
                    Log.Information("[{Source}] {Message}",
                        MethodBase.GetCurrentMethod()?.DeclaringType?.Name,
                        trimLockMsg);

                    await TrimTask(url, trimParams.StartTime, trimParams.EndTime);

                    var trimReleaseMsg =
                        $"{Context.User.Username}#{Context.User.Discriminator} released: {url} ({trimParams.StartTime} - {trimParams.EndTime})";
                    Log.Information("[{Source}] {Message}",
                        MethodBase.GetCurrentMethod()?.DeclaringType?.Name,
                        trimReleaseMsg);
                    break;
                }
            case OperationType.Speed:
                {
                    var speedParams = (SpeedParams)operationParams;
                    var speedLockMsg =
                        $"{Context.User.Username}#{Context.User.Discriminator} locked: {url} ({speedParams.Speed})";
                    Log.Information("[{Source}] {Message}",
                        MethodBase.GetCurrentMethod()?.DeclaringType?.Name,
                        speedLockMsg);

                    await SpeedTask(url, speedParams.Speed);

                    var speedReleaseMsg =
                        $"{Context.User.Username}#{Context.User.Discriminator} released: {url} ({speedParams.Speed})";
                    Log.Write(LogEventLevel.Information,
                        "[{Source}] {Message}",
                        MethodBase.GetCurrentMethod()?.DeclaringType?.Name,
                        speedReleaseMsg);
                    break;
                }
        }

    }
}