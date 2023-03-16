using Discord;
using Discord.Interactions;
using DougaBot.PreConditions;
using JetBrains.Annotations;

namespace DougaBot.Modules.DownloadGroup;

public sealed partial class DownloadGroup
{
    /// <summary>
    /// Download Video
    /// </summary>
    [UsedImplicitly]
    [SlashCommand("video", "Download Video")]
    public async Task SlashDownloadVideoCommand(string url)
    {
        await DeferAsync(options: _globalTasks.ReqOptions);

        var downloadResult = await _videoService.DownloadVideoAsync(null, url, null, Context);
        if (downloadResult.filePath is null ||
            downloadResult.compressPath is null)
        {
            RateLimitAttribute.ClearRateLimit(Context.User.Id);
            return;
        }

        var fileSize = new FileInfo(downloadResult.filePath).Length / 1048576f;
        await _globalTasks.UploadFile(fileSize, downloadResult.filePath, Context);
    }

    [UsedImplicitly]
    [MessageCommand("Download Video")]
    public async Task VideoMessageCommand(IMessage message)
    {
        await DeferAsync(options: _globalTasks.ReqOptions);

        if (message.Attachments.Any())
        {
            foreach (var attachment in message.Attachments)
            {
                if (!attachment.ContentType.StartsWith("video"))
                {
                    await FollowupAsync("Invalid file type",
                        ephemeral: true,
                        options: _globalTasks.ReqOptions);
                    return;
                }

                var downloadResult = await _videoService.DownloadVideoAsync(null, attachment.Url, null, Context);
                if (downloadResult.filePath is null ||
                    downloadResult.compressPath is null)
                {
                    RateLimitAttribute.ClearRateLimit(Context.User.Id);
                    return;
                }

                var fileSize = new FileInfo(downloadResult.filePath).Length / 1048576f;
                await _globalTasks.UploadFile(fileSize, downloadResult.filePath, Context);
            }
        }
        else
        {
            var extractUrl = await _globalTasks.ExtractUrl(message.Content, Context.Interaction);

            if (extractUrl is null)
            {
                await FollowupAsync("No URL found",
                    ephemeral: true,
                    options: _globalTasks.ReqOptions);
                return;
            }

            var downloadResult = await _videoService.DownloadVideoAsync(null, extractUrl, null, Context);
            if (downloadResult.filePath is null ||
                downloadResult.compressPath is null)
            {
                RateLimitAttribute.ClearRateLimit(Context.User.Id);
                return;
            }

            var fileSize = new FileInfo(downloadResult.filePath).Length / 1048576f;
            await _globalTasks.UploadFile(fileSize, downloadResult.filePath, Context);
        }
    }
}