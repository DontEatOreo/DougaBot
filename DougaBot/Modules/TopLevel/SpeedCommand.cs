using Discord.Interactions;
using DougaBot.PreConditions;
using Xabe.FFmpeg;
using YoutubeDLSharp.Options;
using static DougaBot.GlobalTasks;

namespace DougaBot.Modules.TopLevel;

public sealed partial class TopLevel
{
    /// <summary>
    /// Change the speed of a video or audio file
    /// </summary>
    [SlashCommand("speed", "Change the speed of a video/audio")]
    public async Task SpeedCmd(string url,
        [Choice("0.5x", 0.5),
         Choice("1.5x", 1.5),
         Choice("2x", 2),
         Choice("4x", 4)] double speed)
        => await DeferAsync(options: Options)
            .ContinueWith(_ => QueueHandler(url,
                OperationType.Speed,
                new SpeedParams { Speed = speed }));

    public async Task SpeedTask(string url, double speed)
    {
        var runFetch = await RunFetch(url,
            TimeSpan.FromHours(2),
            "The Video or Audio needs to be shorter than 2 hours",
            "Couldn't fetch video or audio data",
            Context.Interaction);
        if (runFetch is null)
            return;

        var runDownload = await RunDownload(url,
            "There was an error downloading the file\nPlease try again later",
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
         * I've mention in other code comments that's possible for the library to say one extension and yt-dlp to download another.
         * Over here Speed Command supports both video and audio thus I can't look at content type.
         * Instead we just tell the user we can't speed up the video if there is extension mismatch.
         */
        var folderUuid = Guid.NewGuid().ToString()[..4];
        var beforeFile = Path.Combine(DownloadFolder, $"{runFetch.ID}.{runFetch.Extension}");
        if (!File.Exists(beforeFile))
        {
            await FollowupAsync("Couldn't speed up the file\nPlease try again later",
                ephemeral: true,
                options: Options);
            return;
        }
        var afterFile = Path.Combine(DownloadFolder, folderUuid, $"{runFetch.ID}.mp4");

        var beforeStreamInfo = await FFmpeg.GetMediaInfo(beforeFile);
        var videoStream = beforeStreamInfo.VideoStreams.FirstOrDefault();
        var audioStream = beforeStreamInfo.AudioStreams.FirstOrDefault();

        if (videoStream is null && audioStream is null)
        {
            await FollowupAsync("Invalid file",
                ephemeral: true,
                options: Options);
            return;
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
        var afterFileSize = afterMediaInfo.Size / 1048576f;

        if (afterMediaInfo.Duration > TimeSpan.FromHours(2))
        {
            File.Delete(afterFile);
            if (videoStream is not null)
                await FollowupAsync("The Video needs to be shorter than 2 hours",
                    ephemeral: true,
                    options: Options);
            else
                await FollowupAsync("The Audio needs to be shorter than 2 hours",
                    ephemeral: true,
                    options: Options);
            return;
        }

        await UploadFile(afterFileSize, afterFile, Context);
    }
}