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
        await DeferAsync(options: _globals.ReqOptions);
        Uri.TryCreate(url, UriKind.Absolute, out var uri);

        if (uri == null && attachment == null)
        {
            const string message = "You need to provide either a url or an attachment";
            await FollowupAsync(message, ephemeral: true, options: _globals.ReqOptions);
            RateLimitService.Clear(RateLimitService.RateLimitType.User, Context.Guild.Id, MethodBase.GetCurrentMethod()!.Name);
            return;
        }

        if (uri != null && attachment != null)
        {
            const string message = "You can't provide both a url and an attachment";
            await FollowupAsync(message, ephemeral: true, options: _globals.ReqOptions);
            RateLimitService.Clear(RateLimitService.RateLimitType.User, Context.Guild.Id, MethodBase.GetCurrentMethod()!.Name);
            return;
        }

        if (attachment is not null)
        {
            url = attachment.Url;
            Uri.TryCreate(url, UriKind.Absolute, out uri);
        }

        using var lockAsync = await _asyncKeyedLocker.LockAsync(Key);

        var remainingCount = _asyncKeyedLocker.GetRemainingCount(Key);
        if (remainingCount > 1)
        {
            var message = $"Your audio is in position {remainingCount} in the queue.";
            await FollowupAsync(message, ephemeral: true, options: _globals.ReqOptions);
        }

        var (downloadPath, compressPath) = await _audioService.Download(uri!, Context);
        if (downloadPath is null || compressPath is null)
        {
            RateLimitService.Clear(RateLimitService.RateLimitType.User, Context.Guild.Id, MethodBase.GetCurrentMethod()!.Name);
            return;
        }

        await _audioService.Compress(downloadPath, compressPath, bitrate);
        var fileSize = new FileInfo(compressPath).Length * _globals.BytesInMegabyte;
        await _globals.UploadAsync(fileSize, compressPath, Context);
    }
}