using Discord;
using Discord.Interactions;
using DougaBot.PreConditions;
using JetBrains.Annotations;

namespace DougaBot.Modules.CompressGroup;

public sealed partial class CompressGroup
{
    /// <summary>
    /// Compress a video
    /// </summary>
    [UsedImplicitly]
    [SlashCommand("video", "Compress Video")]
    public async Task SlashCompressVideoCommand(IAttachment? attachment = null,
        string? url = null,
        [Choice("144p", "144p"),
         Choice("240p", "240p"),
         Choice("360p", "360p"),
         Choice("480p", "480p"),
         Choice("720p", "720p")]
        string? resolution = default)
    {
        await DeferAsync(options: _globalTasks.ReqOptions);

        using var lockAsync = await _asyncKeyedLocker.LockAsync(Key);

        if (url is null && attachment is null)
        {
            await FollowupAsync("You need to provide either a url or an attachment",
                ephemeral: true,
                options: _globalTasks.ReqOptions)
                .ConfigureAwait(false);
            RateLimitAttribute.ClearRateLimit(Context.User.Id);
            return;
        }
        if (url is not null && attachment is not null)
        {
            await FollowupAsync("You can't provide both a url and an attachment",
                ephemeral: true,
                options: _globalTasks.ReqOptions)
                .ConfigureAwait(false);
            RateLimitAttribute.ClearRateLimit(Context.User.Id);
            return;
        }

        var remainingCount = _asyncKeyedLocker.GetRemainingCount(Key);
        if (remainingCount > 0)
        {
            // Send a message to the user about the queue position. You might need to update this part based on your implementation.
            await FollowupAsync(
                $"Your video is in position {_asyncKeyedLocker.GetRemainingCount(Key)} in the queue.",
                ephemeral: true,
                options: _globalTasks.ReqOptions)
                .ConfigureAwait(false);
        }

        var downloadResult = await _videoService.DownloadVideoAsync(attachment, url, resolution, Context);
        if (downloadResult.filePath is null ||
            downloadResult.compressPath is null)
        {
            RateLimitAttribute.ClearRateLimit(Context.User.Id);
            return;
        }

        var compressResult = await _videoService.CompressVideoAsync(downloadResult.filePath, downloadResult.compressPath, downloadResult.resolution, downloadResult.resolutionChange, Context);
        var fileSize = new FileInfo(compressResult.outputPath).Length / 1048576f;
        await _globalTasks.UploadFile(fileSize, compressResult.outputPath, Context);
    }
}