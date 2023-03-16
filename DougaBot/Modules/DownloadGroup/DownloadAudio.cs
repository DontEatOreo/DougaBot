using Discord;
using Discord.Interactions;
using DougaBot.PreConditions;
using JetBrains.Annotations;

namespace DougaBot.Modules.DownloadGroup;

public sealed partial class DownloadGroup
{

    /// <summary>
    /// Download Audio
    /// </summary>
    [UsedImplicitly]
    [SlashCommand("audio", "Download Audio")]
    public async Task SlashCompressAudioCommand(string url)
    {
        await DeferAsync(options: _globalTasks.ReqOptions);

        var downloadResult = await _audioService.DownloadAudioAsync(null, url, Context);
        if (downloadResult.filePath is null)
        {
            RateLimitAttribute.ClearRateLimit(Context.User.Id);
            return;
        }

        var fileSize = new FileInfo(downloadResult.filePath).Length / 1048576f;
        await _globalTasks.UploadFile(fileSize, downloadResult.filePath, Context);
    }

    [UsedImplicitly]
    public async Task AudioMessageCommand(IMessage message)
    {
        await DeferAsync(options: _globalTasks.ReqOptions);

        if (message.Attachments.Any())
        {
            foreach (var attachment in message.Attachments)
            {
                if (!attachment.ContentType.StartsWith("audio") ||
                   !attachment.ContentType.StartsWith("video"))
                {
                    await FollowupAsync("Invalid file type",
                        ephemeral: true,
                        options: _globalTasks.ReqOptions);
                    return;
                }

                var downloadResult = await _audioService.DownloadAudioAsync(attachment, null, Context);
                if (downloadResult.filePath is null)
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

            var downloadResult = await _audioService.DownloadAudioAsync(null, extractUrl, Context);
            if (downloadResult.filePath is null)
            {
                RateLimitAttribute.ClearRateLimit(Context.User.Id);
                return;
            }
            var fileSize = new FileInfo(downloadResult.filePath).Length / 1048576f;
            await _globalTasks.UploadFile(fileSize, downloadResult.filePath, Context);
        }
    }
}