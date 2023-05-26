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
        await DeferAsync(options: _globals.ReqOptions);
        Uri.TryCreate(url, UriKind.Absolute, out var uri);

        if (uri is null && attachment?.Url is null)
        {
            const string message = "You need to provide either a url or an attachment";
            await FollowupAsync(message, ephemeral: true, options: _globals.ReqOptions);
            RateLimitService.Clear(RateLimitService.RateLimitType.User, Context.Guild.Id, MethodBase.GetCurrentMethod()!.Name);
            return;
        }

        if (url != null && attachment?.Url != null)
        {
            const string message = "You can't provide both a url and an attachment";
            await FollowupAsync(message, ephemeral: true, options: _globals.ReqOptions);
            RateLimitService.Clear(RateLimitService.RateLimitType.User, Context.Guild.Id, MethodBase.GetCurrentMethod()!.Name);
            return;
        }

        if (attachment != null)
        {
            url = attachment.Url;
            Uri.TryCreate(url, UriKind.Absolute, out uri);
        }

        using var lockAsync = await _asyncKeyedLocker.LockAsync(Key);
        var remainingCount = _asyncKeyedLocker.GetRemainingCount(Key);
        if (remainingCount > 1)
        {
            var message = $"Your file is in position {_asyncKeyedLocker.GetRemainingCount(Key)} in the queue.";
            await FollowupAsync(message, ephemeral: true, options: _globals.ReqOptions);
        }

        var result = await _speedService.Speed(uri!, speed, Context);
        if (result is null)
        {
            RateLimitService.Clear(RateLimitService.RateLimitType.User, Context.User.Id, MethodBase.GetCurrentMethod()!.Name);
            return;
        }

        var fileSize = new FileInfo(result).Length / _globals.BytesInMegabyte;
        await _globals.UploadAsync(fileSize, result, Context);
    }
}