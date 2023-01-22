using System.Globalization;
using System.Text.RegularExpressions;
using Discord.Interactions;
using DougaBot.PreConditions;
using YoutubeDLSharp.Options;
using static DougaBot.GlobalTasks;

namespace DougaBot.Modules.TopLevel;

public sealed partial class TopLevel
{
    [GeneratedRegex(@"^\d{1,6}(\.\d{1,2})?")]
    private static partial Regex TrimTimeRegex();

    /// <summary>
    /// Trim a video or audio
    /// </summary>
    [RateLimit(15)]
    [SlashCommand("trim", "Trim a video or audio")]
    public async Task TrimCommand(string url,
        [Summary(description: "Format: ss.ms (seconds.milliseconds)")] string startTime,
        [Summary(description: "Format: ss.ms (seconds.milliseconds)")] string endTime)
        => await DeferAsync(options: Options)
            .ContinueWith(_ => QueueHandler(url,
                OperationType.Trim,
                new TrimParams
                {
                    StartTime = startTime,
                    EndTime = endTime
                }));

    public async Task TrimTask(string url, string startTime, string endTime)
    {
        var startTimeFloat = float.Parse(startTime, CultureInfo.InvariantCulture);
        var endTimeFloat = float.Parse(endTime, CultureInfo.InvariantCulture);

        if (Math.Abs(startTimeFloat - endTimeFloat) < 1)
        {
            await FollowupAsync("Start time and end time cannot be less than 1 second apart",
                ephemeral: true,
                options: Options);
            return;
        }

        if (startTimeFloat > endTimeFloat)
        {
            await FollowupAsync("Start time cannot be greater than end time");
            return;
        }

        var runFetch = await RunFetch(url, TimeSpan.FromMinutes(5),
            "Video is too long.\nThe video needs to be shorter than 5 minutes",
            "Could not fetch video data",
            Context.Interaction);
        if (runFetch is null)
            return;

        var videoDuration = runFetch.Duration;
        if (startTimeFloat > videoDuration)
        {
            await FollowupAsync("Start time cannot be greater than video duration",
                ephemeral: true,
                options: Options);
            return;
        }
        if (endTimeFloat > videoDuration)
        {
            await FollowupAsync("End time cannot be greater than video duration",
                ephemeral: true,
                options: Options);
            return;
        }

        var folderUuid = Guid.NewGuid().ToString()[..4];

        var runDownload = await RunDownload(url,
            "There was an error trimming the video\nPlease try again later",
            new OptionSet
            {
                FormatSort = FormatSort,
                DownloadSections = $"*{startTimeFloat}-{endTimeFloat}",
                ForceKeyframesAtCuts = true,
                NoPlaylist = true,
                Output = Path.Combine(DownloadFolder, folderUuid, "%(id)s.%(ext)s")
            }, Context.Interaction);

        if (!runDownload)
        {
            RateLimitAttribute.ClearRateLimit(Context.User.Id);
            return;
        }

        var trimFile = Directory.GetFiles(Path.Combine(DownloadFolder, folderUuid)).FirstOrDefault();
        if (trimFile is null)
        {
            await FollowupAsync("Couldn't process video",
                ephemeral: true,
                options: Options);
            return;
        }

        var fileSize = new FileInfo(trimFile).Length / 1048576f;

        await UploadFile(fileSize, trimFile, Context);
    }
}