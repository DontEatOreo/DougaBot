using System.Reflection;
using Discord;
using Discord.Interactions;
using DougaBot.Services.RateLimit;
using JetBrains.Annotations;

namespace DougaBot.Modules.CompressGroup;

public sealed partial class CompressGroup
{
    /// <summary>
    /// Compress Audio
    /// </summary>
    [UsedImplicitly]
    [SlashCommand("audio", "Compress Audio")]
    public async Task SlashCompressAudioCommand(
        [Choice("96k", 96)]
        [Choice("128k", 128)]
        [Choice("192k", 192)]
        [Choice("256k", 256)]
        [Choice("320k", 320)]
        int bitrate, string? url = null, IAttachment? attachment = null)
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
                $"Your audio is in position {_asyncKeyedLocker.GetRemainingCount(Key)} in the queue.",
                ephemeral: true,
                options: _globals.ReqOptions)
                .ConfigureAwait(false);
        }

        var downloadResult = await _audioService.Download(url ?? attachment?.Url, Context).ConfigureAwait(false);
        if (downloadResult.filePath is null || downloadResult.outPath is null)
        {
            RateLimitService.Clear(RateLimitService.RateLimitType.User,
                Context.Guild.Id,
                MethodBase.GetCurrentMethod()!.Name);
            return;
        }

        await _audioService.Compress(downloadResult.filePath, downloadResult.outPath, bitrate).ConfigureAwait(false);
        var fileSize = new FileInfo(downloadResult.outPath).Length / 1024 / 1024;
        await _globals.UploadAsync(fileSize, downloadResult.outPath, Context).ConfigureAwait(false);
    }
}