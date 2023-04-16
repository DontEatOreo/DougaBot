using System.Reflection;
using Discord;
using Discord.Interactions;
using DougaBot.Services.RateLimit;
using JetBrains.Annotations;

namespace DougaBot.Modules.CompressGroup;

public sealed partial class CompressGroup
{
    public enum Resolution
    {
        [ChoiceDisplay("144p")]
        [UsedImplicitly]
        P144,

        [ChoiceDisplay("240p")]
        [UsedImplicitly]
        P240,

        [ChoiceDisplay("360p")]
        [UsedImplicitly]
        P360,

        [ChoiceDisplay("480p")]
        [UsedImplicitly]
        P480,

        [ChoiceDisplay("720p")]
        [UsedImplicitly]
        P720,

        [ChoiceDisplay("No Change")]
        [UsedImplicitly]
        None
    }

    [SlashCommand("video", "Compress Video")]
    [UsedImplicitly]
    public async Task SlashCompressVideoCommand(IAttachment? attachment = null,
        string? url = null,
        Resolution resolution = Resolution.None)
    {
        await DeferAsync(options: _globals.ReqOptions).ConfigureAwait(false);

        using var lockAsync = await _asyncKeyedLocker.LockAsync(Key).ConfigureAwait(false);

        if (url is null && attachment is null)
        {
            await FollowupAsync("You need to provide either a url or an attachment",
                ephemeral: true,
                options: _globals.ReqOptions)
                .ConfigureAwait(false);
            RateLimitService.Clear(RateLimitService.RateLimitType.Guild, Context.Guild.Id, MethodBase.GetCurrentMethod()!.Name);
            return;
        }
        if (url != null && attachment != null)
        {
            await FollowupAsync("You can't provide both a url and an attachment",
                ephemeral: true,
                options: _globals.ReqOptions)
                .ConfigureAwait(false);
            RateLimitService.Clear(RateLimitService.RateLimitType.Guild, Context.Guild.Id, MethodBase.GetCurrentMethod()!.Name);
            return;
        }

        var remainingCount = _asyncKeyedLocker.GetRemainingCount(Key);
        if (remainingCount > 1)
        {
            await FollowupAsync(
                $"Your video is in position {_asyncKeyedLocker.GetRemainingCount(Key)} in the queue.",
                ephemeral: true,
                options: _globals.ReqOptions)
                .ConfigureAwait(false);
        }

        var download = await _videoService.Download((url ?? attachment?.Url)!, Context).ConfigureAwait(false);
        if (download.filePath is null ||
            download.outPath is null)
        {
            RateLimitService.Clear(RateLimitService.RateLimitType.User, Context.User.Id, MethodBase.GetCurrentMethod()!.Name);
            return;
        }

        var compress = await _videoService.Compress(download.filePath,
            download.outPath,
            resolution,
            Context).ConfigureAwait(false);

        if (compress is null)
        {
            RateLimitService.Clear(RateLimitService.RateLimitType.User, Context.User.Id, MethodBase.GetCurrentMethod()!.Name);
            return;
        }

        var fileSize = new FileInfo(compress).Length / _globals.BytesInMegabyte;
        await _globals.UploadAsync(fileSize, compress, Context).ConfigureAwait(false);
    }
}