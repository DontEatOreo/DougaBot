using System.Globalization;
using Discord.Interactions;
using YoutubeDLSharp.Options;

namespace DougaBot.Services.Trim;

public class TrimService : InteractionModuleBase<SocketInteractionContext>, ITrimService
{
    #region Constructor

    private readonly Globals _globals;

    public TrimService(Globals globals)
    {
        _globals = globals;
    }

    #endregion

    #region Methods

    public async Task<string?>
        Trim(string url, string startTime, string endTime, SocketInteractionContext context)
    {
        var startTimeFloat = float.Parse(startTime, CultureInfo.InvariantCulture);
        var endTimeFloat = float.Parse(endTime, CultureInfo.InvariantCulture);

        if (Math.Abs(startTimeFloat - endTimeFloat) < 1)
        {
            await FollowupAsync("Start time and end time cannot be less than 1 second apart",
                ephemeral: true,
                options: _globals.ReqOptions)
                .ConfigureAwait(false);
            return default;
        }

        if (startTimeFloat > endTimeFloat)
        {
            await FollowupAsync("Start time cannot be greater than end time")
                .ConfigureAwait(false);
            return default;
        }

        var runFetch = await _globals.FetchAsync(url, TimeSpan.FromHours(2),
            "Video is too long.\nThe video needs to be shorter than 2 hours",
            "Could not fetch video data",
            context.Interaction).ConfigureAwait(false);
        if (runFetch is null)
            return default;

        var videoDuration = runFetch.Duration;
        if (startTimeFloat > videoDuration)
        {
            await context.Interaction.FollowupAsync("Start time cannot be greater than video duration",
                ephemeral: true,
                options: _globals.ReqOptions)
                .ConfigureAwait(false);
            return default;
        }
        if (endTimeFloat > videoDuration)
        {
            await context.Interaction.FollowupAsync("End time cannot be greater than video duration",
                ephemeral: true,
                options: _globals.ReqOptions)
                .ConfigureAwait(false);
            return default;
        }

        var folderUuid = Guid.NewGuid().ToString()[..4];

        await context.Interaction.FollowupAsync("Please wait while the video is being trimmed...",
            ephemeral: true,
            options: _globals.ReqOptions)
            .ConfigureAwait(false);

        var downloadArgs =
            $"*{startTimeFloat.ToString(CultureInfo.InvariantCulture)}-{endTimeFloat.ToString(CultureInfo.InvariantCulture)}";
        var runDownload = await _globals.DownloadAsync(url,
            "There was an error trimming.\nPlease try again later",
            new OptionSet
            {
                FormatSort = _globals.FormatSort,
                DownloadSections = downloadArgs,
                ForceKeyframesAtCuts = true,
                NoPlaylist = true,
                Output = Path.Combine(_globals.DownloadFolder, folderUuid, "%(id)s.%(ext)s")
            }, context.Interaction).ConfigureAwait(false);

        if (!runDownload)
            return default;

        var trimFile = Directory.GetFiles(Path.Combine(_globals.DownloadFolder, folderUuid)).FirstOrDefault();
        if (trimFile is not null)
            return trimFile;

        await context.Interaction.FollowupAsync("Couldn't process video",
            ephemeral: true,
            options: _globals.ReqOptions)
            .ConfigureAwait(false);
        return default;
    }

    #endregion
}