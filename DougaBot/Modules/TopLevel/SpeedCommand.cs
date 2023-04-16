using System.Reflection;
using Discord;
using Discord.Interactions;
using DougaBot.Services.RateLimit;
using JetBrains.Annotations;

namespace DougaBot.Modules.TopLevel;

public sealed partial class TopLevelGroup
{
    [UsedImplicitly]
    [RateLimit(60)]
    [SlashCommand("speed", "Change the speed of a video/audio")]
    public async Task SpeedCmd(
        [Choice("0.5x", 0.5)]
        [Choice("1.5x", 1.5)]
        [Choice("2x", 2)]
        [Choice("4x", 4)] double speed,
        string? url = null, IAttachment? attachment = null)
    {
        await DeferAsync(options: _globals.ReqOptions).ConfigureAwait(false);

        if (url is null && attachment?.Url is null)
        {
            await FollowupAsync("You need to provide either a url or an attachment",
                    ephemeral: true,
                    options: _globals.ReqOptions)
                .ConfigureAwait(false);
            RateLimitService.Clear(RateLimitService.RateLimitType.User,
                Context.Guild.Id,
                MethodBase.GetCurrentMethod()!.Name);
            return;
        }

        if (url != null && attachment?.Url != null)
        {
            await FollowupAsync("You can't provide both a url and an attachment",
                ephemeral: true,
                options: _globals.ReqOptions).ConfigureAwait(false);
            RateLimitService.Clear(RateLimitService.RateLimitType.User,
                Context.Guild.Id,
                MethodBase.GetCurrentMethod()!.Name);
            return;
        }

        using var lockAsync = await _asyncKeyedLocker.LockAsync(Key).ConfigureAwait(false);

        var remainingCount = _asyncKeyedLocker.GetRemainingCount(Key);
        if (remainingCount > 1)
        {
            await FollowupAsync(
                    $"Your file is in position {_asyncKeyedLocker.GetRemainingCount(Key)} in the queue.",
                    ephemeral: true,
                    options: _globals.ReqOptions)
                .ConfigureAwait(false);
        }

        var result = await _speedService.Speed(url ?? attachment?.Url, speed, Context).ConfigureAwait(false);
        if (result is null)
        {
            RateLimitService.Clear(RateLimitService.RateLimitType.User, Context.User.Id, MethodBase.GetCurrentMethod()!.Name);
            return;
        }

        var fileSize = new FileInfo(result).Length / _globals.BytesInMegabyte;
        await _globals.UploadAsync(fileSize, result, Context).ConfigureAwait(false);
    }
}