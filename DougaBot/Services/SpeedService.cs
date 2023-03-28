using Discord.Interactions;
using DougaBot.PreConditions;
using Serilog;
using Xabe.FFmpeg;
using YoutubeDLSharp.Options;
using static DougaBot.GlobalTasks;

namespace DougaBot.Services;

public class SpeedService : InteractionModuleBase<SocketInteractionContext>, ISpeedService
{
    #region Constructor

    private readonly GlobalTasks _globalTasks;

    public SpeedService(GlobalTasks globalTasks)
    {
        _globalTasks = globalTasks;
    }

    #endregion

    #region Methods

    public async Task<string?> SpeedTaskAsync(string url, double speed, SocketInteractionContext context)
    {
        var runFetch = await _globalTasks.RunFetch(url,
            TimeSpan.FromHours(2),
            "The Video or Audio needs to be shorter than 2 hours",
            "Couldn't fetch video or audio data",
            context.Interaction);
        if (runFetch is null)
        {
            RateLimitAttribute.ClearRateLimit(Context.User.Id);
            return default;
        }

        var runDownload = await _globalTasks.RunDownload(url,
            "There was an error downloading the file\nPlease try again later",
            new OptionSet
            {
                FormatSort = FormatSort,
                NoPlaylist = true
            }, context.Interaction);
        if (!runDownload)
        {
            RateLimitAttribute.ClearRateLimit(Context.User.Id);
            return default;
        }

        /*
         * It's possible for the library to say one extension and yt-dlp to download another.
         * Over here Speed Command supports both video and audio thus I can't look at content type.
         * Instead we just tell the user we can't speed up the video if there is extension mismatch.
         */
        var folderUuid = Guid.NewGuid().ToString()[..4];
        var beforeFile = Path.Combine(DownloadFolder, $"{runFetch.ID}.{runFetch.Extension}");
        if (!File.Exists(beforeFile))
        {
            await FollowupAsync("Couldn't speed up the file\nPlease try again later",
                ephemeral: true,
                options: _globalTasks.ReqOptions)
                .ConfigureAwait(false);
            return default;
        }
        var afterFile = Path.Combine(DownloadFolder, folderUuid, $"{runFetch.ID}.mp4");

        var beforeStreamInfo = await FFmpeg.GetMediaInfo(beforeFile);
        var videoStream = beforeStreamInfo.VideoStreams.FirstOrDefault();
        var audioStream = beforeStreamInfo.AudioStreams.FirstOrDefault();

        if (videoStream is null && audioStream is null)
        {
            await context.Interaction.FollowupAsync("Invalid file",
                ephemeral: true,
                options: _globalTasks.ReqOptions);
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

        await conversion.Start();

        var afterMediaInfo = await FFmpeg.GetMediaInfo(afterFile);

        if (afterMediaInfo.Duration <= TimeSpan.FromHours(2))
            return afterFile;

        try
        {
            Directory.Delete(Path.Combine(DownloadFolder, folderUuid), true);
        }
        catch (Exception e)
        {
            Log.Error(e, "Couldn't delete folder");
        }

        if (videoStream is not null)
            await context.Interaction.FollowupAsync("The Video needs to be shorter than 2 hours",
                ephemeral: true,
                options: _globalTasks.ReqOptions)
                .ConfigureAwait(false);
        else
            await context.Interaction.FollowupAsync("The Audio needs to be shorter than 2 hours",
                ephemeral: true,
                options: _globalTasks.ReqOptions)
                .ConfigureAwait(false);
        return default;
    }

    #endregion
}