using System.Diagnostics;
using System.Reflection;
using Discord.Interactions;
using DougaBot.PreConditions;
using Serilog;
using Xabe.FFmpeg;
using YoutubeDLSharp.Options;
using static DougaBot.GlobalTasks;

namespace DougaBot.Modules.CompressGroup;

public sealed partial class CompressGroup
{
    private async Task VideoQueueHandler(string url, string before, string after, string? resolution, bool resolutionChange)
    {
        var userLock = QueueLocks.GetOrAdd(Context.User.Id, _ => new SemaphoreSlim(1, 1));

        await userLock.WaitAsync();
        try
        {
            Log.Information(
                "[{Source}] {Message}",
                MethodBase.GetCurrentMethod()?.DeclaringType?.Name,
                $"{Context.User.Username}#{Context.User.Discriminator} ({Context.User.Id}) locked: {url}");
            await CompressVideo(before, after, resolution, resolutionChange);
        }
        catch (Exception e)
        {
            Log.Error(e, "[{Source}] {Message}",
                MethodBase.GetCurrentMethod()?.DeclaringType?.Name,
                e.Message);
        }
        finally
        {
            Log.Information(
                "[{Source}] {Message}",
                MethodBase.GetCurrentMethod()?.DeclaringType?.Name,
                $"{Context.User.Username}#{Context.User.Discriminator} ({Context.User.Id}) finished: {url}");
            userLock.Release();
        }
    }

    /// <summary>
    /// Compress a video
    /// </summary>
    [SlashCommand("video", "Compress Video")]
    public async Task SlashCompressVideoCommand(string url,
        [Choice("144p", "144p"),
         Choice("240p", "240p"),
         Choice("360p", "360p"),
         Choice("720p", "720p")]
        string? resolution = default)
        => await DeferAsync(options: Options)
            .ContinueWith(async _ => await DownloadVideo(url, resolution));

    private async Task DownloadVideo(string url, string? resolution)
    {
        // if resolution is empty set resolutionChange then false
        var resolutionChange = !string.IsNullOrEmpty(resolution);

        var runResult = await RunDownload(url, TimeSpan.FromMinutes(4),
            "Video is too long.\nThe video needs to be shorter than 4 minutes",
            "Could not fetch video data",
            "There was an error downloading the video\nPlease try again later",
            new OptionSet
            {
                FormatSort = FormatSort,
                NoPlaylist = true,
                // RemuxVideo = RemuxVideo
            }, Context.Interaction);

        if (runResult is null)
        {
            RateLimitAttribute.ClearRateLimit(Context.User.Id);
            return;
        }

        var folderUuid = Guid.NewGuid().ToString()[..4];
        var before = Path.Combine(DownloadFolder, $"{runResult.ID}.{runResult.Extension}");
        var after = Path.Combine(DownloadFolder, folderUuid, $"{runResult.ID}.mp4");

        // get video duration of beforeVideo
        var beforeDuration = FFmpeg.GetMediaInfo(before).Result.Duration;

        if (beforeDuration > TimeSpan.FromMinutes(4))
        {
            await FollowupAsync("Video is too long.\nThe video needs to be shorter than 4 minutes", ephemeral: true,
                options: Options);
            File.Delete(before);
            return;
        }

        await VideoQueueHandler(url, before, after, resolution, resolutionChange);
    }

    private async Task CompressVideo(string before, string after, string? resolution, bool resolutionChange)
    {
        var mediaInfo = await FFmpeg.GetMediaInfo(before);
        var videoStream = mediaInfo.VideoStreams.FirstOrDefault();
        var audioStream = mediaInfo.AudioStreams.FirstOrDefault();

        if (videoStream is null)
        {
            File.Delete(before);
            await FollowupAsync("Could not find video stream",
                ephemeral: true,
                options: Options);
            Log.Warning("[{Source}] {File} has no video streams found",
                MethodBase.GetCurrentMethod()?.DeclaringType?.Name,
                before);
            return;
        }

        // Get the dimensions of the input video
        var originalWidth = videoStream.Width;
        var originalHeight = videoStream.Height;
        var outputWidth = 0;
        var outputHeight = 0;

        /*
		 * This if statement, adjusts the output resolution of a video based on its original aspect ratio.
		 * It uses Math.Sign() to check if the video is landscape (width > height), portrait (width < height) or square (width = height).
		 * If it's landscape, we use the full width and adjust the height to maintain aspect ratio.
		 * The formula used for this is (originalHeight * outputWidth / originalWidth) which is used to calculate the new height based on the given width and original aspect ratio.
		 * If it's portrait, we use the full height and adjust the width to maintain aspect ratio using the formula (originalWidth * outputHeight / originalHeight)
		 * If it's square, we use the full width and height of the selected resolution
		 * We also make sure the width and height are rounded down to the nearest multiple of 2 (ffmpeg only allows resolutions that can be divided by 2)
		 */

        if (resolutionChange)
        {
            var resolutionChoices = typeof(CompressGroup)
                .GetMethod(nameof(SlashCompressVideoCommand))
                ?.GetParameters()
                .Where(x => x.CustomAttributes.Any(y => y.AttributeType == typeof(ChoiceAttribute)))
                .SelectMany(x => x.GetCustomAttributes<ChoiceAttribute>())
                .Select(x => x.Value)
                .ToList();

            // Calculate the dimensions of the output video based on the original dimensions and the selected resolution
            var resolutionMap = resolutionChoices!.ToDictionary(x => x.ToString()!,
                    x => (Width: int.Parse(x.ToString()?[..^1]!), Height: int.Parse(x.ToString()?[..^1]!)));
            outputWidth = resolutionMap[resolution!].Width;
            outputHeight = resolutionMap[resolution!].Height;

            switch (Math.Sign(originalWidth - originalHeight))
            {
                case 1:
                    // If the input video is landscape orientation, use the full width and adjust the height
                    outputHeight = (int)Math.Round(originalHeight * ((double)outputWidth / originalWidth));
                    // Round down the width and height to the nearest multiple of 2
                    outputWidth -= outputWidth % 2;
                    outputHeight -= outputHeight % 2;
                    break;
                case -1:
                    // If the input video is portrait orientation, use the full height and adjust the width
                    outputWidth = (int)Math.Round(originalWidth * ((double)outputHeight / originalHeight));
                    // Round down the width and height to the nearest multiple of 2
                    outputWidth -= outputWidth % 2;
                    outputHeight -= outputHeight % 2;
                    break;
                default:
                    // If the input video is square, use the full width and height of the selected resolution
                    outputWidth = resolutionMap[resolution!].Width;
                    outputHeight = resolutionMap[resolution!].Height;
                    break;
            }
        }

        if (outputHeight > 720)
        {
            outputWidth = (int)Math.Round(originalWidth * ((double)720 / originalHeight));
            // Round down the width and height to the nearest multiple of 2
            outputWidth -= outputWidth % 2;
            outputHeight = 720;
        }

        if (outputHeight > 0)
            videoStream.SetSize(outputWidth, outputHeight);
        videoStream.SetCodec(VideoCodec.h264);

        // Compress Video
        var conversion = FFmpeg.Conversions.New()
            .AddStream(videoStream)
            .SetOutput(after)
            .SetPreset(ConversionPreset.VerySlow)
            .SetPixelFormat(PixelFormat.yuv420p)
            .SetPriority(ProcessPriorityClass.BelowNormal)
            .UseMultiThread(Environment.ProcessorCount > 1 ? Environment.ProcessorCount : 1)
            .AddParameter("-crf 30");

        if (Convert.ToBoolean(Environment.GetEnvironmentVariable("USE_HARDWARE_ACCELERATION")))
            conversion.UseHardwareAcceleration(HardwareAccelerator.auto, VideoCodec.h264, VideoCodec.h264);

        if (audioStream is not null)
        {
            conversion.AddStream(audioStream);
            audioStream.SetCodec(AudioCodec.aac);
            if (audioStream.Bitrate > 128)
                audioStream.SetBitrate(128);
        }

        await conversion.Start();
        var videoSize = new FileInfo(after).Length / 1048576f;
        await UploadFile(videoSize, after, Context);
    }
}