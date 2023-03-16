using Discord;
using Discord.Interactions;
using DougaBot.PreConditions;
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
        [Choice("64k", 64)]
        [Choice("96k", 96)]
        [Choice("128k", 128)]
        [Choice("192k", 192)]
        [Choice("256k", 256)]
        [Choice("320k", 320)]
        int bitrate, string? url = null, IAttachment? attachment = null)
    {
        await DeferAsync(options: _globalTasks.ReqOptions);

        using var lockAsync = await _asyncKeyedLocker.LockAsync(Key);

        if (url is null && attachment is null)
        {
            await FollowupAsync("You need to provide either a url or an attachment",
                ephemeral: true,
                options: _globalTasks.ReqOptions);
            RateLimitAttribute.ClearRateLimit(Context.User.Id);
            return;
        }

        if (url is not null && attachment is not null)
        {
            await FollowupAsync("You can't provide both a url and an attachment",
                ephemeral: true,
                options: _globalTasks.ReqOptions);
            RateLimitAttribute.ClearRateLimit(Context.User.Id);
            return;
        }

        var remainingCount = _asyncKeyedLocker.GetRemainingCount(Key);
        if (remainingCount > 0)
        {
            // Send a message to the user about the queue position. You might need to update this part based on your implementation.
            await FollowupAsync(
                $"Your audio is in position {_asyncKeyedLocker.GetRemainingCount(Key)} in the queue.",
                ephemeral: true,
                options: _globalTasks.ReqOptions);
        }

        var downloadResult = await _audioService.DownloadAudioAsync(attachment, url, Context);
        if (downloadResult.filePath is null || downloadResult.compressPath is null)
        {
            RateLimitAttribute.ClearRateLimit(Context.User.Id);
            return;
        }

        var compressResult =
            await _audioService.CompressAudio(downloadResult.filePath, downloadResult.compressPath, bitrate, Context);
        var fileSize = new FileInfo(downloadResult.compressPath).Length / 1048576f;

        await _globalTasks.UploadFile(fileSize, compressResult.compressPath, Context);
    }
}