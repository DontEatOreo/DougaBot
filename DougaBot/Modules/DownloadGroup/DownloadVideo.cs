using System.Reflection;
using Discord;
using Discord.Interactions;
using DougaBot.PreConditions;
using Microsoft.AspNetCore.StaticFiles;
using Serilog;
using Xabe.FFmpeg;
using YoutubeDLSharp.Metadata;
using YoutubeDLSharp.Options;
using static DougaBot.GlobalTasks;

namespace DougaBot.Modules.DownloadGroup;

public sealed partial class DownloadGroup
{
    /// <summary>
    /// Download Video
    /// </summary>
    [SlashCommand("video", "Download Video")]
    public async Task SlashDownloadVideoCommand(string url)
        => await DeferAsync(options: Options)
            .ContinueWith(async _ => await DownloadVideo(url));

    [MessageCommand("Download Video")]
    public async Task VideoMessageCommand(IMessage message)
    {
        await DeferAsync(options: Options);

        if (message.Attachments.Any())
        {
            foreach (var attachment in message.Attachments)
            {
                if (!attachment.ContentType.StartsWith("video"))
                {
                    await FollowupAsync("Invalid file type",
                        ephemeral: true,
                        options: Options);
                    return;
                }

                await DownloadVideo(attachment.Url);
            }
        }
        else
        {
            var extractUrl = await ExtractUrl(message.Content, Context.Interaction);

            if (extractUrl is null)
            {
                await FollowupAsync("No URL found",
                    ephemeral: true,
                    options: Options);
                return;
            }

            await DownloadVideo(extractUrl);
        }
    }

    private async Task DownloadVideo(string url)
    {
        var runFetch = await RunFetch(url, TimeSpan.FromHours(2),
            "Video needs to be shorter than 2 hours",
            "Could not download video",
            Context.Interaction);
        if (runFetch is null)
            return;

        var runDownload = await RunDownload(url,
            "Couldn't download video",
            new OptionSet
            {
                FormatSort = FormatSort,
                NoPlaylist = true
            }, Context.Interaction);

        if (!runDownload)
        {
            RateLimitAttribute.ClearRateLimit(Context.User.Id);
            return;
        }

        /*
         * Previously I used to use `runFetch.Extension` but they're cases where the library returns one extension, but yt-dlp downloads a different one.
         * To combat this, I'm looking for first file in the directory that matches the video ID with any extension that is of type video.
         * The reason I'm using content type is because a user could first download an audio file,
         * then download a video file with the same ID, and then the audio file would be sent, instead of the video file.
         * If you have any other better ideas, please let me know.
         */
        var videoPath = Directory.GetFiles(DownloadFolder, $"{runFetch.ID}.*")
            .FirstOrDefault(x => new FileExtensionContentTypeProvider().TryGetContentType(x, out var contentType) && contentType.StartsWith("video"));
        if (videoPath is null)
        {
            await FollowupAsync("Couldn't process video",
                ephemeral: true,
                options: Options);
            return;
        }

        var videoSize = new FileInfo(videoPath).Length / 1048576f;
        if (videoSize > 1024)
        {
            File.Delete(videoPath);
            await FollowupAsync("Video is too big.\nThe video needs to be smaller than 1GB",
                ephemeral: true,
                options: Options);
            Log.Warning("[{Source}] {File} is too big",
                MethodBase.GetCurrentMethod()?.Name,
                runFetch.ID);
            return;
        }

        await UploadFile(videoSize, videoPath, Context);
    }
}