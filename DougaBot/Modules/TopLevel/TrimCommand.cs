using System.Globalization;
using System.Text.RegularExpressions;
using Discord.Interactions;
using DougaBot.PreConditions;
using YoutubeDLSharp.Options;
using static DougaBot.GlobalTasks;

namespace DougaBot.Modules.TopLevel;

public sealed partial class TopLevel
{
    [GeneratedRegex(@"^\d{1,4}(\.\d{1,2})?$")]
    private static partial Regex TrimTimeRegex();

    /// <summary>
    /// Trim a video or audio
    /// </summary>
    [RateLimit(15)]
    [SlashCommand("trim", "Trim a video or audio")]
    public async Task TrimCommand(string url,
        [Summary(description: "Format: ss.ms (seconds.milliseconds)")] float startTime,
        [Summary(description: "Format: ss.ms (seconds.milliseconds)")] float endTime)
        => await DeferAsync(options: Options)
            .ContinueWith(_ => QueueHandler(url,
                OperationType.Trim,
                new TrimParams
                {
                    StartTime = startTime,
                    EndTime = endTime
                }));

    public async Task TrimTask(string url, float startTime, float endTime)
    {
        if (startTime > endTime)
        {
            await FollowupAsync("Start time cannot be greater than end time");
            return;
        }

        var runVideoDataFetch = await YoutubeDl.RunVideoDataFetch(url, flat: true);
        if (!runVideoDataFetch.Success)
        {
            await FollowupAsync("Please provide a valid URL",
                ephemeral: true,
                options: Options);
            return;
        }

        var videoDuration = runVideoDataFetch.Data.Duration;
        if (startTime > videoDuration)
        {
            await FollowupAsync("Start time cannot be greater than video duration",
                ephemeral: true,
                options: Options);
            return;
        }
        if (endTime > videoDuration)
        {
            await FollowupAsync("End time cannot be greater than video duration",
                ephemeral: true,
                options: Options);
            return;
        }

        var folderUuid = Guid.NewGuid().ToString()[..4];
        var runDownload = await RunDownload(url,
            TimeSpan.FromHours(2),
            "The Video or Audio needs to be shorter than 2 hours",
            "Couldn't fetch video or audio data",
            "There was an error trimming the video\nPlease try again later",
            new OptionSet
            {
                FormatSort = FormatSort,
                DownloadSections =
                    $"*{startTime.ToString(CultureInfo.InvariantCulture)}-{endTime.ToString(CultureInfo.InvariantCulture)}",
                ForceKeyframesAtCuts = true,
                NoPlaylist = true,
                Output = Path.Combine(DownloadFolder, folderUuid, "%(id)s.%(ext)s")
            }, Context.Interaction);

        if (runDownload is null)
        {
            RateLimitAttribute.ClearRateLimit(Context.User.Id);
            return;
        }

        var trimFile = Path.Combine(DownloadFolder, folderUuid, $"{runDownload.ID}.{runDownload.Extension}");
        var fileSize = new FileInfo(trimFile).Length / 1048576f;

        await UploadFile(fileSize, trimFile, Context);
    }
}