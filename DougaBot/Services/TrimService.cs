using System.Globalization;
using Discord.Interactions;
using DougaBot.PreConditions;
using YoutubeDLSharp.Options;
using static DougaBot.GlobalTasks;

namespace DougaBot.Services;

public class TrimService : InteractionModuleBase<SocketInteractionContext>, ITrimService
{
    #region Constructor

    private readonly GlobalTasks _globalTasks;

    public TrimService(GlobalTasks globalTasks)
    {
        _globalTasks = globalTasks;
    }

    #endregion

    #region Methods

    public async Task<string?>
        TrimTaskAsync(string url, string startTime, string endTime, SocketInteractionContext context)
    {
        var startTimeFloat = float.Parse(startTime, CultureInfo.InvariantCulture);
        var endTimeFloat = float.Parse(endTime, CultureInfo.InvariantCulture);

        if (Math.Abs(startTimeFloat - endTimeFloat) < 1)
        {
            await FollowupAsync("Start time and end time cannot be less than 1 second apart",
                ephemeral: true,
                options: _globalTasks.ReqOptions);
            return default;
        }

        if (startTimeFloat > endTimeFloat)
        {
            await FollowupAsync("Start time cannot be greater than end time");
            return default;
        }

        var runFetch = await _globalTasks.RunFetch(url, TimeSpan.FromHours(2),
            "Video is too long.\nThe video needs to be shorter than 2 hours",
            "Could not fetch video data",
            context.Interaction);
        if (runFetch is null)
        {
            RateLimitAttribute.ClearRateLimit(context.User.Id);
            return default;
        }

        var videoDuration = runFetch.Duration;
        if (startTimeFloat > videoDuration)
        {
            await context.Interaction.FollowupAsync("Start time cannot be greater than video duration",
                ephemeral: true,
                options: _globalTasks.ReqOptions);
            return default;
        }
        if (endTimeFloat > videoDuration)
        {
            await context.Interaction.FollowupAsync("End time cannot be greater than video duration",
                ephemeral: true,
                options: _globalTasks.ReqOptions);
            return default;
        }

        var folderUuid = Guid.NewGuid().ToString()[..4];

        await context.Interaction.FollowupAsync("Please wait while the video is being trimmed...",
            ephemeral: true,
            options: _globalTasks.ReqOptions);

        var downloadArgs =
            $"*{startTimeFloat.ToString(CultureInfo.InvariantCulture)}-{endTimeFloat.ToString(CultureInfo.InvariantCulture)}";
        var runDownload = await _globalTasks.RunDownload(url,
            "There was an error trimming.\nPlease try again later",
            new OptionSet
            {
                FormatSort = $"{FormatSort},ext:mp4",
                DownloadSections = downloadArgs,
                ForceKeyframesAtCuts = true,
                NoPlaylist = true,
                Output = Path.Combine(DownloadFolder, folderUuid, "%(id)s.%(ext)s")
            }, context.Interaction);

        if (!runDownload)
        {
            RateLimitAttribute.ClearRateLimit(context.User.Id);
            return default;
        }

        var trimFile = Directory.GetFiles(Path.Combine(DownloadFolder, folderUuid)).FirstOrDefault();
        if (trimFile is not null)
            return trimFile;

        await context.Interaction.FollowupAsync("Couldn't process video",
            ephemeral: true,
            options: _globalTasks.ReqOptions);
        return default;
    }

    #endregion
}