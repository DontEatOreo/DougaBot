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
                new SpeedParams
                {
                    Speed = speed
                }));

    public async Task SpeedTask(string url, double speed)
    {
        var runDownload = await RunDownload(url, TimeSpan.FromHours(2),
            "The Video or Audio needs to be shorter than 2 hours",
            "Couldn't fetch video or audio data",
            "There was an error downloading the file\nPlease try again later",
            new OptionSet
            {
                FormatSort = FormatSort,
                NoPlaylist = true,
            }, Context.Interaction);
        if (runDownload is null)
        {
            RateLimitAttribute.ClearRateLimit(Context.User.Id);
            return;
        }

        var folderUuid = Guid.NewGuid().ToString()[..4];
        var beforeFile = Path.Combine(DownloadFolder, $"{runDownload.ID}.{runDownload.Extension}");
        var afterFile = Path.Combine(DownloadFolder, folderUuid, $"{runDownload.ID}.mp4");

        var beforeStreamInfo = await FFmpeg.GetMediaInfo(beforeFile);
        var videoStream = beforeStreamInfo.VideoStreams.FirstOrDefault();
        var audioStream = beforeStreamInfo.AudioStreams.FirstOrDefault();

        if (videoStream is null && audioStream is null)
        {
            await FollowupAsync("Invalid file", ephemeral: true, options: Options);
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
                await FollowupAsync("The Video needs to be shorter than 2 hours", ephemeral: true, options: Options);
            else
                await FollowupAsync("The Audio needs to be shorter than 2 hours", ephemeral: true, options: Options);
            return;
        }

        await UploadFile(afterFileSize, afterFile, Context);
    }
}