using System.Reflection;
using Discord.Interactions;
using DougaBot.Services.RateLimit;
using JetBrains.Annotations;

namespace DougaBot.Modules.TopLevel;

public sealed partial class TopLevelGroup
{
    [RateLimit(15)]
    [SlashCommand("trim", "Trim a video or audio")]
    [UsedImplicitly]
    public async Task TrimCommand(string url,
        [Summary(description: "Format: ss.ms (seconds.milliseconds)")] string startTime,
        [Summary(description: "Format: ss.ms (seconds.milliseconds)")] string endTime)
    {
        await DeferAsync(options: _globals.ReqOptions).ConfigureAwait(false);

        using var lockAsync = await _asyncKeyedLocker.LockAsync(url).ConfigureAwait(false);

        var remainingCount = _asyncKeyedLocker.GetRemainingCount(Key);
        if (remainingCount > 1)
        {
            await FollowupAsync(
                    $"Your file is in position {_asyncKeyedLocker.GetRemainingCount(Key)} in the queue.",
                    ephemeral: true,
                    options: _globals.ReqOptions)
                .ConfigureAwait(false);
        }

        var trimPath = await _trimService.Trim(url, startTime, endTime, Context).ConfigureAwait(false);
        if (trimPath is null)
        {
            RateLimitService.Clear(RateLimitService.RateLimitType.User, Context.User.Id, MethodBase.GetCurrentMethod()!.Name);
            return;
        }

        var fileSize = new FileInfo(trimPath).Length / _globals.BytesInMegabyte;
        await _globals.UploadAsync(fileSize, trimPath, Context).ConfigureAwait(false);
    }
}