using System.Reflection;
using Discord.Interactions;
using DougaBot.PreConditions;
using Microsoft.AspNetCore.StaticFiles;
using Serilog;
using Xabe.FFmpeg;
using YoutubeDLSharp.Options;
using static DougaBot.GlobalTasks;

namespace DougaBot.Modules.CompressGroup;

public sealed partial class CompressGroup
{
    private bool _iOsCompatible = Convert.ToBoolean(Environment.GetEnvironmentVariable("IOS_COMPATIBLE"));

    private readonly string[] _vp9Args = {
        "-row-mt 1",
        "-lag-in-frames 25",
        "-cpu-used 4",
        "-auto-alt-ref 1",
        "-arnr-maxframes 7",
        "-arnr-strength 4",
        "-aq-mode 0",
        "-enable-tpl 1",
        "-row-mt 1"
    };

    /// <summary>
    /// Compress a video
    /// </summary>
    [SlashCommand("video", "Compress Video")]
    public async Task SlashCompressVideoCommand(string url,
        [Choice("144p", "144p"),
         Choice("240p", "240p"),
         Choice("360p", "360p"),
         Choice("480p", "480p"),
         Choice("720p", "720p")]
        string? resolution = default)
        => await DeferAsync(options: Options)
            .ContinueWith(async _ => await DownloadVideo(url, resolution));

    private async Task DownloadVideo(string url, string? resolution)
    {
        var resolutionChange = resolution is not null;

        var runFetch = await RunFetch(url, TimeSpan.FromMinutes(5),
            "Video is too long.\nThe video needs to be shorter than 5 minutes",
            "Could not fetch video data",
            Context.Interaction);
        if (runFetch is null)
            return;

        var runDownload = await RunDownload(url,
            "There was an error downloading the video\nPlease try again later",
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
         * Previously I used to use `runFetch.Extension` but they're cases where the library returns one extension, but yt-dlp downloads a different one.
         * To combat this, I'm looking for first file in the directory that matches the video ID with any extension that is of type video.
         * The reason I'm using content type is because a user could first download an audio file,
         * then download a video file with the same ID, and then the audio file would be sent, instead of the video file.
         * If you have any other better ideas, please let me know.
         */
        string? before = null;
        string? after = null;
        var folderUuid = Guid.NewGuid().ToString()[..4];
        var matchingFiles = Directory.GetFiles(DownloadFolder, $"{runFetch.ID}.*");
        foreach (var matchingFile in matchingFiles)
        {
            if (!new FileExtensionContentTypeProvider()
                    .TryGetContentType(matchingFile, out var contentType) || !contentType.StartsWith("video"))
                continue;

            var extension = Path.GetExtension(matchingFile);
            after = Path.Combine(DownloadFolder, folderUuid, $"{runFetch.ID}.{extension}");
            before = matchingFile;
        }
        if (before is null || after is null)
        {
            await FollowupAsync("Couldn't process video",
                ephemeral: true,
                options: Options);
            return;
        }

        var beforeDuration = await FFmpeg.GetMediaInfo(before);

        if (beforeDuration.Duration > TimeSpan.FromMinutes(4))
        {
            await FollowupAsync("Video is too long.\n" +
                                "The video needs to be shorter than 4 minutes",
                ephemeral: true,
                options: Options);
            File.Delete(before);
            return;
        }

        VideoCompressionParams videoParams = new() { Resolution = resolution, ResolutionChange = resolutionChange };
        await CompressionQueueHandler(url, before, after, CompressionType.Video, videoParams);
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
            var resolutionTuple = GetOutputDimensions(originalWidth, originalHeight, resolution!);
            outputHeight = resolutionTuple.Height;
            outputWidth = resolutionTuple.Width;
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

        // Compress Video
        var conversion = FFmpeg.Conversions.New()
            .AddStream(videoStream)
            .SetOutput(after)
            .SetPreset(ConversionPreset.VerySlow)
            .SetPixelFormat(PixelFormat.yuv420p10le)
            .AddParameter("-crf 30");

        if (_iOsCompatible)
            videoStream.SetCodec(VideoCodec.libx264);
        else
        {
            videoStream.SetCodec(VideoCodec.vp9);
            conversion.AddParameter(string.Join(" ", _vp9Args));
        }


        if (audioStream is not null)
        {
            conversion.AddStream(audioStream);
            audioStream.SetCodec(!_iOsCompatible
                ? AudioCodec.opus
                : AudioCodec.aac);
            if (audioStream.Bitrate > 128)
                audioStream.SetBitrate(128);
        }

        await conversion.Start();

        var videoSize = new FileInfo(after).Length / 1048576f;
        await UploadFile(videoSize, after, Context);
    }

    private static (int Width, int Height) GetOutputDimensions(int originalWidth, int originalHeight, string resolution)
    {
        var width = int.Parse(resolution[..^1]);

        int outputWidth, outputHeight;
        if (originalWidth > originalHeight)
        {
            // If the input video is landscape orientation, use the full width and adjust the height
            outputWidth = width;
            outputHeight = (int)Math.Round(originalHeight * ((double)outputWidth / originalWidth));
        }
        else if (originalWidth < originalHeight)
        {
            // If the input video is portrait orientation, use the full height and adjust the width
            outputHeight = width;
            outputWidth = (int)Math.Round(originalWidth * ((double)outputHeight / originalHeight));
        }
        else
        {
            // If the input video is square, use the full width and height of the selected resolution
            outputWidth = width;
            outputHeight = width;
        }

        // Round down the width and height to the nearest multiple of 2
        outputWidth -= outputWidth % 2;
        outputHeight -= outputHeight % 2;

        return (outputWidth, outputHeight);
    }
}