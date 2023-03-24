using System.Reflection;
using Discord;
using Discord.Interactions;
using DougaBot.PreConditions;
using Microsoft.AspNetCore.StaticFiles;
using Serilog;
using Xabe.FFmpeg;
using YoutubeDLSharp.Options;
using static DougaBot.GlobalTasks;

namespace DougaBot.Services;

public class VideoService : InteractionModuleBase<SocketInteractionContext>, IVideoService
{
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

    private readonly bool _iOsCompatible = Convert.ToBoolean(Environment.GetEnvironmentVariable("IOS_COMPATIBLE"));

    #region Constructor

    private readonly IContentTypeProvider _contentTypeProvider;
    private readonly GlobalTasks _globalTasks;

    public VideoService(IContentTypeProvider contentTypeProvider, GlobalTasks globalTasks)
    {
        _contentTypeProvider = contentTypeProvider;
        _globalTasks = globalTasks;
    }

    #endregion

    #region Methods

    public async ValueTask<(string? filePath, string? compressPath, string? resolution, bool resolutionChange, SocketInteractionContext? context)>
        DownloadVideoAsync(IAttachment? attachment, string? url, string? resolution, SocketInteractionContext context)
    {
        var resolutionChange = resolution is not null;

        var runFetch = await _globalTasks.RunFetch(url ?? attachment!.Url, TimeSpan.FromMinutes(10),
            "Video is too long.\nThe video needs to be shorter than 10 minutes",
            "Could not fetch video data",
            context.Interaction);
        if (runFetch is null)
        {
            RateLimitAttribute.ClearRateLimit(context.User.Id);
            return default;
        }

        var runDownload = await _globalTasks.RunDownload(url ?? attachment!.Url,
            "There was an error downloading the video\nPlease try again later",
            new OptionSet
            {
                FormatSort = FormatSort,
                NoPlaylist = true
            }, context.Interaction);

        if (!runDownload)
        {
            RateLimitAttribute.ClearRateLimit(context.User.Id);
            return default;
        }

        string videoPath = null!;
        string compressPath = null!;
        var folderUuid = Guid.NewGuid().ToString()[..4];
        var matchingFiles = Directory.GetFiles(DownloadFolder, $"{runFetch.ID}.*");
        foreach (var matchingFile in matchingFiles)
        {
            if (!_contentTypeProvider.TryGetContentType(matchingFile, out var contentType) ||
                !contentType.StartsWith("video"))
                continue;

            var extension = Path.GetExtension(matchingFile);
            videoPath = matchingFile;
            compressPath = Path.Combine(DownloadFolder, folderUuid, $"{runFetch.ID}{extension}");
        }

        var videoDuration = await FFmpeg.GetMediaInfo(videoPath);
        var videoStream = videoDuration.VideoStreams.FirstOrDefault();

        if (videoStream is null)
        {
            File.Delete(videoPath);
            await FollowupAsync("Could not find video stream",
                ephemeral: true,
                options: _globalTasks.ReqOptions);
            Log.Warning("[{Source}] {File} has no video streams found",
                MethodBase.GetCurrentMethod()?.DeclaringType?.Name,
                videoPath);
            return default;
        }

        if (videoDuration.Duration <= TimeSpan.FromMinutes(4))
            return (videoPath, compressPath, resolution, resolutionChange, context);

        await FollowupAsync("Video is too long.\n" +
                            "The video needs to be shorter than 4 minutes",
            ephemeral: true,
            options: _globalTasks.ReqOptions);
        return default;
    }

    public async Task<(string videoPath, string outputPath, SocketInteractionContext context)>
        CompressVideoAsync(string videoPath, string outputPath, string? resolution, bool resolutionChange, SocketInteractionContext context)
    {
        Log.Information("[{Source}] {File} is being compressed",
            MethodBase.GetCurrentMethod()?.DeclaringType?.Name,
            videoPath);

        var inputVideoInfo = await FFmpeg.GetMediaInfo(videoPath);
        var videoStream = inputVideoInfo.VideoStreams.FirstOrDefault();
        var audioStream = inputVideoInfo.AudioStreams.FirstOrDefault();

        if (videoStream is null)
        {
            File.Delete(videoPath);
            await FollowupAsync("Could not find video stream",
                ephemeral: true,
                options: _globalTasks.ReqOptions);
            Log.Warning("[{Source}] {File} has no video streams found",
                MethodBase.GetCurrentMethod()?.DeclaringType?.Name,
                videoPath);
            return default;
        }

        if (videoStream.Height > 720)
            await SetResolutionAsync(videoStream, videoStream.Width, 720);

        if (resolutionChange)
            await CalculateResolutionAsync(videoStream, resolution!);

        var conversion = FFmpeg.Conversions.New()
            .AddStream(videoStream)
            .SetOutput(outputPath)
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
                ? AudioCodec.libopus
                : AudioCodec.aac);
            if (audioStream.Bitrate > 128)
                audioStream.SetBitrate(128);
        }

        Log.Information("[{Source}] Starting conversion of {File}",
            MethodBase.GetCurrentMethod()?.DeclaringType?.Name,
            videoPath);
        await conversion.Start();
        Log.Information("[{Source}] Finished conversion of {File}",
            MethodBase.GetCurrentMethod()?.DeclaringType?.Name,
            videoPath);

        return (videoPath, outputPath, context);
    }

    public ValueTask CalculateResolutionAsync(IVideoStream videoStream, string resolution)
    {
        double originalWidth = videoStream.Width;
        double originalHeight = videoStream.Height;

        // Parse the resolution input string (remove the "p" suffix)
        var resolutionInt = int.Parse(resolution[..^1]);

        // Calculate the aspect ratio of the input video
        var aspectRatio = originalWidth / originalHeight;

        // Calculate the output width and height based on the desired resolution and aspect ratio
        var outputWidth = (int)Math.Round(resolutionInt * aspectRatio);
        var outputHeight = resolutionInt;

        // Round the output width and height to even numbers
        outputWidth -= outputWidth % 2;
        outputHeight -= outputHeight % 2;

        // Set the output size
        videoStream.SetSize(outputWidth, outputHeight);
        return default;
    }

    public ValueTask SetResolutionAsync(IVideoStream videoStream, int inputWidth, int inputHeight)
    {
        // Round the output width and height to even numbers
        inputWidth -= inputWidth % 2;
        inputHeight -= inputHeight % 2;

        // Set the output size
        videoStream.SetSize(inputWidth, inputHeight);
        return default;
    }

    #endregion
}