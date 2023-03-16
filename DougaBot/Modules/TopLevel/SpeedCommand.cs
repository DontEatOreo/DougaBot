using Discord.Interactions;
using DougaBot.PreConditions;
using JetBrains.Annotations;

namespace DougaBot.Modules.TopLevel;

public sealed partial class TopLevelGroup
{
    [UsedImplicitly]
    [RateLimit(30, RateLimitAttribute.RateLimitType.Global)]
    [SlashCommand("speed", "Change the speed of a video/audio")]
    public async Task SpeedCmd(string url,
        [Choice("0.5x", 0.5),
         Choice("1.5x", 1.5),
         Choice("2x", 2),
         Choice("4x", 4)] double speed)
    {
        await DeferAsync(options: _globalTasks.ReqOptions);
        using var lockAsync = await _asyncKeyedLocker.LockAsync(url).ConfigureAwait(false);
        var speedResult = await _speedService.SpeedTaskAsync(url, speed, Context);
        if (speedResult is null)
        {
            RateLimitAttribute.ClearRateLimit(Context.User.Id);
            return;
        }
        var fileSize = new FileInfo(speedResult).Length / 1048576f;
        await _globalTasks.UploadFile(fileSize, speedResult, Context);
    }
}