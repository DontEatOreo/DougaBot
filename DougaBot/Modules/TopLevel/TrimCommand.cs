using System.Reflection;
using System.Text.RegularExpressions;
using Discord.Interactions;
using DougaBot.PreConditions;
using Serilog;
using YoutubeDLSharp.Options;
using static DougaBot.GlobalTasks;

namespace DougaBot.Modules.TopLevel;

public sealed partial class TopLevel
{
    [GeneratedRegex(@"^(?:\d{1,2}:)?(?:\d{1,2}:)?(\d{1,2})(?:\.(\d{1,3}))?$")]
    private static partial Regex TrimTimeRegex();

    private async Task TrimQueueHandler(string url, string startTime, string endTime)
    {
        var userLock = QueueLocks.GetOrAdd(Context.User.Id, _ => new SemaphoreSlim(1, 1));

        await userLock.WaitAsync();
        try
        {
            Log.Information("[{Source}] {Message}",
                MethodBase.GetCurrentMethod()?.DeclaringType?.Name,
                $"{Context.User.Username}#{Context.User.Discriminator} locked: {url} ({startTime} - {endTime})");
            await TrimTask(url, startTime, endTime);
        }
        catch (Exception e)
        {
            Log.Error("[{Source}] {Message}", MethodBase.GetCurrentMethod()?.DeclaringType?.Name, e.Message);
        }
        finally
        {
            Log.Information("[{Source}] {Message}",
                MethodBase.GetCurrentMethod()?.DeclaringType?.Name,
                $"{Context.User.Username}#{Context.User.Discriminator} released: {url} ({startTime} - {endTime})");
            userLock.Release();
        }
    }

    /// <summary>
    /// Trim a video or audio
    /// </summary>
    [RateLimit(15)]
    [SlashCommand("trim", "Trim a video or audio")]
    public async Task TrimCommand()
        => await RespondWithModalAsync<TrimModal>("trim_command_modal");

    [ModalInteraction("trim_command_modal")]
    public async Task TrimModalInteraction(TrimModal modal)
        => await DeferAsync(options: Options)
            .ContinueWith(async _ => await TrimQueueHandler(modal.Url, modal.StartTime, modal.EndTime));

    public class TrimModal : IModal
    {
        public string Title => "Trim Command";

        [InputLabel("URL")]
        [ModalTextInput("trim_url", placeholder: "https://youtu.be/MYPVQccHhAQ", maxLength: 300)]
        // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Global
        public string Url { get; set; } = string.Empty;

        [InputLabel("Start Time")]
        [ModalTextInput("start_time",
            placeholder: "Format: hh:mm:ss.ms or ss.ms",
            maxLength: 11)]
        // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Global
        public string StartTime { get; set; } = string.Empty;

        [InputLabel("End Time")]
        [ModalTextInput("end_time",
            placeholder: "Format: hh:mm:ss.ms or ss.ms",
            maxLength: 11)]
        // ReSharper disable once AutoPropertyCanBeMadeGetOnly.Global
        public string EndTime { get; set; } = string.Empty;
    }

    private async Task TrimTask(string url, string startTime, string endTime)
    {
        var startTimeSeconds = await ParseTime(startTime);
        var endTimeSeconds = await ParseTime(endTime);

        if (startTimeSeconds == TimeSpan.MaxValue ||
            endTimeSeconds == TimeSpan.MaxValue)
            return;

        if (startTimeSeconds.TotalSeconds > endTimeSeconds.TotalSeconds)
        {
            await FollowupAsync(
                "Start time cannot be greater than end time",
                ephemeral: true,
                options: Options);
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
        if (startTimeSeconds.TotalSeconds > videoDuration ||
            endTimeSeconds.TotalSeconds > videoDuration)
        {
            await FollowupAsync("Start or end time cannot be greater than video duration",
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
                DownloadSections = $"*{startTime}-{endTime}",
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

    private async Task<TimeSpan> ParseTime(string time)
    {
        var timeFormatMatch = TrimTimeRegex().Match(time);
        if (!timeFormatMatch.Success)
        {
            await FollowupAsync("Invalid start time format",
                ephemeral: true,
                options: Options);
            return TimeSpan.MaxValue;
        }

        var seconds = int.Parse(timeFormatMatch.Groups[1].Value);
        var milliseconds = timeFormatMatch.Groups[2].Value;

        milliseconds = !string.IsNullOrEmpty(milliseconds)
            ? milliseconds.PadRight(3, '0')
            : "0";

        return new TimeSpan(0, 0, 0, seconds, int.Parse(milliseconds));
    }
}