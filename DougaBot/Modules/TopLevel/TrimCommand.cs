using Discord.Interactions;
using DougaBot.PreConditions;
using JetBrains.Annotations;

namespace DougaBot.Modules.TopLevel;

public sealed partial class TopLevelGroup
{
    [UsedImplicitly]
    [RateLimit(15, RateLimitAttribute.RateLimitType.Global)]
    [SlashCommand("trim", "Trim a video or audio")]
    public async Task TrimCommand(string url,
        [Summary(description: "Format: ss.ms (seconds.milliseconds)")] string startTime,
        [Summary(description: "Format: ss.ms (seconds.milliseconds)")] string endTime)
    {
        await DeferAsync(options: _globalTasks.ReqOptions);
        using var lockAsync = await _asyncKeyedLocker.LockAsync(url).ConfigureAwait(false);

        var trimResult = await _trimService.TrimTaskAsync(url, startTime, endTime, Context);
        if (trimResult is null)
        {
            RateLimitAttribute.ClearRateLimit(Context.User.Id);
            return;
        }

        var fileSize = new FileInfo(trimResult).Length / 1048576f;
        await _globalTasks.UploadFile(fileSize, trimResult, Context);
    }
}