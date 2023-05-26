using System.Reflection;
using Discord;
using Discord.Interactions;
using DougaBot.Services.RateLimit;
using JetBrains.Annotations;

namespace DougaBot.Modules.CompressGroup;

public sealed partial class CompressGroup
{
    [SlashCommand("video", "Compress Video")]
    [UsedImplicitly]
    public async Task SlashCompressVideoCommand(IAttachment? attachment = null,
        string? url = null,
        Resolution resolution = Resolution.None)
    {
        await DeferAsync(options: _globals.ReqOptions);
        Uri.TryCreate(url, UriKind.Absolute, out var uri);

        using var lockAsync = await _asyncKeyedLocker.LockAsync(Key);

        if (uri is null && attachment is null)
        {
            const string error = "You need to provide either a url or an attachment";
            await FollowupAsync(error, ephemeral: true, options: _globals.ReqOptions);
            RateLimitService.Clear(RateLimitService.RateLimitType.Guild, Context.Guild.Id, MethodBase.GetCurrentMethod()!.Name);
            return;
        }
        if (uri != null && attachment != null)
        {
            const string error = "You can't provide both a url and an attachment";
            await FollowupAsync(error, ephemeral: true, options: _globals.ReqOptions);
            RateLimitService.Clear(RateLimitService.RateLimitType.Guild, Context.Guild.Id, MethodBase.GetCurrentMethod()!.Name);
            return;
        }

        if (attachment is not null)
        {
            url = attachment.Url;
            Uri.TryCreate(url, UriKind.Absolute, out uri);
        }

        var remainingCount = _asyncKeyedLocker.GetRemainingCount(Key);
        if (remainingCount > 1)
        {
            var position = $"Your video is in position {_asyncKeyedLocker.GetRemainingCount(Key)} in the queue.";
            await FollowupAsync(position, ephemeral: true, options: _globals.ReqOptions);
        }

        var (downloadPath, compressPath) = await _videoService.Download(uri!, Context);
        if (downloadPath == null || compressPath == null)
        {
            RateLimitService.Clear(RateLimitService.RateLimitType.User, Context.User.Id, MethodBase.GetCurrentMethod()!.Name);
            return;
        }

        var compress = await _videoService.Compress(downloadPath, compressPath, resolution);
        if (compress is null)
        {
            RateLimitService.Clear(RateLimitService.RateLimitType.User, Context.User.Id, MethodBase.GetCurrentMethod()!.Name);
            return;
        }

        var fileSize = new FileInfo(compress).Length / _globals.BytesInMegabyte;
        await _globals.UploadAsync(fileSize, compress, Context);
    }

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
}