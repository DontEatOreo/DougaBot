using Discord.Interactions;
using Microsoft.Extensions.Options;
using Xabe.FFmpeg;
using YoutubeDLSharp.Options;

namespace DougaBot.Services.Speed;

public class SpeedService : InteractionModuleBase<SocketInteractionContext>, ISpeedService
{
    private readonly Globals _globals;

    public SpeedService(Globals globals)
    {
        _globals = globals;
    }

    #region Strings

    private static readonly TimeSpan DurationLimit = TimeSpan.FromHours(2);
    private readonly string _durationErrorMessage = $"The Video or Audio needs to be shorter than {DurationLimit:g}";
    private const string DataFetchErrorMessage = "Couldn't fetch video or audio data";
    private const string DownloadErrorMessage = "There was an error speeding up the video.\nPlease try again later";

    #endregion Strings

    #region Methods

    public async Task<string?> Speed(Uri url, double speed, SocketInteractionContext context)
    {
        var runFetch = await _globals.FetchAsync(url,
            DurationLimit,
            _durationErrorMessage,
            DataFetchErrorMessage,
            context.Interaction);
        if (runFetch is null)
            return default;

        OptionSet optionSet = new()
        {
            FormatSort = _globals.FormatSort,
            NoPlaylist = true
        };
        var runDownload = await _globals.DownloadAsync(url, DownloadErrorMessage, optionSet, context.Interaction);
        if (!runDownload)
            return default;

        /*
        * It's possible for the library to say one extension and yt-dlp to download another.
        * Over here Speed Command supports both video and audio thus I can't look at content type.
        * Instead we just tell the user we can't speed up the video if there is extension mismatch.
        */
        var folderUuid = Guid.NewGuid().ToString()[..4];
        var beforeFileName = $"{runFetch.ID}.{runFetch.Extension}";
        var beforeFile = Path.Combine(Path.GetTempPath(), beforeFileName);
        if (!File.Exists(beforeFile))
        {
            const string message = "Couldn't speed up the file\nPlease try again later";
            await FollowupAsync(message, ephemeral: true, options: _globals.ReqOptions);
            return default;
        }
        var afterFileName = $"{runFetch.ID}.mp4";
        var afterFile = Path.Combine(Path.GetTempPath(), folderUuid, afterFileName);

        var beforeStreamInfo = await FFmpeg.GetMediaInfo(beforeFile);
        var videoStream = beforeStreamInfo.VideoStreams.FirstOrDefault();
        var audioStream = beforeStreamInfo.AudioStreams.FirstOrDefault();

        if (videoStream is null && audioStream is null)
        {
            const string errorMessage = "Invalid file";
            await context.Interaction.FollowupAsync(errorMessage, ephemeral: true, options: _globals.ReqOptions);
            return default;
        }

        var conversion = FFmpeg.Conversions.New()
            .SetOutput(afterFile);

        if (videoStream != null)
        {
            conversion.AddStream(videoStream);
            videoStream.SetCodec(VideoCodec.libx264);
            videoStream.ChangeSpeed(speed);
        }

        if (videoStream == null)
        {
            afterFileName = $"{runFetch.ID}.mp3";
            afterFile = Path.Combine(Path.GetTempPath(), folderUuid, afterFileName);
            conversion.SetOutput(afterFile);
        }

        if (audioStream != null)
        {
            conversion.AddStream(audioStream);
            audioStream.SetCodec(AudioCodec.aac);
            audioStream.ChangeSpeed(speed);
        }

        await conversion.Start();

        var afterMediaInfo = await FFmpeg.GetMediaInfo(afterFile);

        if (afterMediaInfo.Duration <= DurationLimit)
            return afterFile;

        var outDir = Path.Combine(Path.GetTempPath(), folderUuid);
        Directory.Delete(outDir, true);

        if (videoStream is not null)
        {
            var errorMessage = $"The Video needs to be shorter than {DurationLimit:g}";
            await context.Interaction.FollowupAsync(errorMessage, ephemeral: true, options: _globals.ReqOptions);
        }
        else
        {
            var message = $"The Audio needs to be shorter than {DurationLimit:g}";
            await context.Interaction.FollowupAsync(message, ephemeral: true, options: _globals.ReqOptions);
        }
        return default;
    }

    #endregion
}