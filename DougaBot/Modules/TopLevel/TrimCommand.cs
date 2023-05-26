using System.Reflection;
using Discord;
using Discord.Interactions;
using DougaBot.Services.RateLimit;
using JetBrains.Annotations;

namespace DougaBot.Modules.TopLevel;

public sealed partial class TopLevelGroup
{
    [RateLimit(15)]
    [SlashCommand("trim", "Trim a video or audio")]
    [UsedImplicitly]
    public async Task TrimCommand(string? url,
        IAttachment? attachment,
        [Summary(description: "Format: ss.ms (seconds.milliseconds)")] string startTime,
        [Summary(description: "Format: ss.ms (seconds.milliseconds)")] string endTime)
    {
        await DeferAsync(options: _globals.ReqOptions);
        Uri.TryCreate(url, UriKind.Absolute, out var uri);

        if (uri == null && attachment == null)
        {
            const string message = "You must provide a URL or attachment.";
            await FollowupAsync(message, ephemeral: true, options: _globals.ReqOptions);
            RateLimitService.Clear(RateLimitService.RateLimitType.User, Context.Guild.Id, MethodBase.GetCurrentMethod()!.Name);
            return;
        }

        if (uri != null && attachment != null)
        {
            const string message = "You can't provide both a URL and an attachment.";
            await FollowupAsync(message, ephemeral: true, options: _globals.ReqOptions);
            RateLimitService.Clear(RateLimitService.RateLimitType.User, Context.Guild.Id, MethodBase.GetCurrentMethod()!.Name);
            return;
        }

        using var lockAsync = await _asyncKeyedLocker.LockAsync(Key);

        var remainingCount = _asyncKeyedLocker.GetRemainingCount(Key);
        if (remainingCount > 1)
        {
            var message = $"Your file is in position {remainingCount} in the queue.";
            await FollowupAsync(message, ephemeral: true, options: _globals.ReqOptions);
        }

        var trimPath = await _trimService.Trim(uri!, startTime, endTime, Context);
        if (trimPath is null)
        {
            RateLimitService.Clear(RateLimitService.RateLimitType.User, Context.User.Id, MethodBase.GetCurrentMethod()!.Name);
            return;
        }

        var fileSize = new FileInfo(trimPath).Length / _globals.BytesInMegabyte;
        await _globals.UploadAsync(fileSize, trimPath, Context);
    }
}