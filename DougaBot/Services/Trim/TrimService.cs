using System.Globalization;
using Discord.Interactions;
using YoutubeDLSharp.Options;

namespace DougaBot.Services.Trim;

public class TrimService : InteractionModuleBase<SocketInteractionContext>, ITrimService
{
    private readonly Globals _globals;

    public TrimService(Globals globals)
    {
        _globals = globals;
    }

    #region Strings

    private static readonly TimeSpan DurationLimit = TimeSpan.FromHours(2);
    private readonly string _durationErrorMessage = $"Video is too long.\nThe video needs to be shorter than {DurationLimit:g}";
    private const string DataFetchErrorMessage = "Could not fetch video data";
    private const string DownloadErrorMessage = "There was an error trimming.\nPlease try again late";

    #endregion Strings

    #region Methods

    public async Task<string?>
        Trim(Uri url, string startTime, string endTime, SocketInteractionContext context)
    {
        var startTimeFloat = float.Parse(startTime, CultureInfo.InvariantCulture);
        var endTimeFloat = float.Parse(endTime, CultureInfo.InvariantCulture);

        string? message;
        if (Math.Abs(startTimeFloat - endTimeFloat) < 1)
        {
            message = "Start time and end time cannot be less than 1 second apart";
            await FollowupAsync(message, ephemeral: true, options: _globals.ReqOptions);
            return default;
        }

        if (startTimeFloat > endTimeFloat)
        {
            message = "Start time cannot be greater than end time";
            await FollowupAsync(message, ephemeral: true, options: _globals.ReqOptions);
            return default;
        }

        var runFetch = await _globals.FetchAsync(url,
            DurationLimit,
            _durationErrorMessage,
            DataFetchErrorMessage,
            context.Interaction);
        if (runFetch is null)
            return default;

        var videoDuration = runFetch.Duration;
        if (startTimeFloat > videoDuration)
        {
            await context.Interaction.FollowupAsync("Start time cannot be greater than video duration",
                ephemeral: true,
                options: _globals.ReqOptions);
            return default;
        }
        if (endTimeFloat > videoDuration)
        {
            await context.Interaction.FollowupAsync("End time cannot be greater than video duration",
                ephemeral: true,
                options: _globals.ReqOptions);
            return default;
        }

        const string waitMessage = "Please wait while the video is being trimmed...";
        await context.Interaction.FollowupAsync(waitMessage, ephemeral: true, options: _globals.ReqOptions);

        var folderUuid = Guid.NewGuid().ToString()[..4];
        var downloadArgs =
            $"*{startTimeFloat.ToString(CultureInfo.InvariantCulture)}-{endTimeFloat.ToString(CultureInfo.InvariantCulture)}";
        OptionSet optionSet = new()
        {
            FormatSort = _globals.FormatSort,
            DownloadSections = downloadArgs,
            ForceKeyframesAtCuts = true,
            NoPlaylist = true,
            Output = Path.Combine(Path.GetTempPath(), folderUuid, "%(id)s.%(ext)s")
        };
        var runDownload = await _globals.DownloadAsync(url, DownloadErrorMessage, optionSet, context.Interaction);

        if (!runDownload)
            return default;

        var trimFile = Directory.GetFiles(Path.Combine(Path.GetTempPath(), folderUuid)).FirstOrDefault();
        if (trimFile is not null)
            return trimFile;

        message = "Couldn't process video";
        await context.Interaction.FollowupAsync(message, ephemeral: true, options: _globals.ReqOptions);
        return default;
    }

    #endregion
}