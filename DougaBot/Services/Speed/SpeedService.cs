using Discord.Interactions;
using Xabe.FFmpeg;
using YoutubeDLSharp.Options;

namespace DougaBot.Services.Speed;

public class SpeedService : InteractionModuleBase<SocketInteractionContext>, ISpeedService
{
    #region Constructor

    private readonly Globals _globals;

    public SpeedService(Globals globals)
    {
        _globals = globals;
    }

    #endregion

    #region Methods

    public async Task<string?> Speed(string? url, double speed, SocketInteractionContext context)
    {
        var runFetch = await _globals.FetchAsync(url,
            TimeSpan.FromHours(2),
            "The Video or Audio needs to be shorter than 2 hours",
            "Couldn't fetch video or audio data",
            context.Interaction).ConfigureAwait(false);
        if (runFetch is null)
            return default;

        var runDownload = await _globals.DownloadAsync(url,
            "There was an error downloading the file\nPlease try again later",
            new OptionSet
            {
                FormatSort = _globals.FormatSort,
                NoPlaylist = true
            }, context.Interaction).ConfigureAwait(false);
        if (!runDownload)
            return default;

        /*
        * It's possible for the library to say one extension and yt-dlp to download another.
        * Over here Speed Command supports both video and audio thus I can't look at content type.
        * Instead we just tell the user we can't speed up the video if there is extension mismatch.
        */
        var folderUuid = Guid.NewGuid().ToString()[..4];
        var beforeFile = Path.Combine(_globals.DownloadFolder, $"{runFetch.ID}.{runFetch.Extension}");
        if (!File.Exists(beforeFile))
        {
            await FollowupAsync("Couldn't speed up the file\nPlease try again later",
                ephemeral: true,
                options: _globals.ReqOptions)
                .ConfigureAwait(false);
            return default;
        }
        var afterFile = Path.Combine(_globals.DownloadFolder, folderUuid, $"{runFetch.ID}.mp4");

        var beforeStreamInfo = await FFmpeg.GetMediaInfo(beforeFile).ConfigureAwait(false);
        var videoStream = beforeStreamInfo.VideoStreams.FirstOrDefault();
        var audioStream = beforeStreamInfo.AudioStreams.FirstOrDefault();

        if (videoStream is null && audioStream is null)
        {
            await context.Interaction.FollowupAsync("Invalid file",
                ephemeral: true,
                options: _globals.ReqOptions).ConfigureAwait(false);
            return default;
        }

        var conversion = FFmpeg.Conversions.New()
            .SetOutput(afterFile);

        if (videoStream is not null)
        {
            conversion.AddStream(videoStream);
            videoStream.SetCodec(VideoCodec.libx264);
            videoStream.ChangeSpeed(speed);
        }

        if (audioStream is not null)
        {
            conversion.AddStream(audioStream);
            audioStream.SetCodec(AudioCodec.aac);
            audioStream.ChangeSpeed(speed);
        }

        await conversion.Start().ConfigureAwait(false);

        var afterMediaInfo = await FFmpeg.GetMediaInfo(afterFile).ConfigureAwait(false);

        if (afterMediaInfo.Duration <= TimeSpan.FromHours(2))
            return afterFile;

        var outDir = Path.Combine(_globals.DownloadFolder, folderUuid);
        Directory.Delete(outDir, true);

        if (videoStream is not null)
            await context.Interaction.FollowupAsync("The Video needs to be shorter than 2 hours",
                ephemeral: true,
                options: _globals.ReqOptions)
                .ConfigureAwait(false);
        else
            await context.Interaction.FollowupAsync("The Audio needs to be shorter than 2 hours",
                ephemeral: true,
                options: _globals.ReqOptions)
                .ConfigureAwait(false);
        return default;
    }

    #endregion
}