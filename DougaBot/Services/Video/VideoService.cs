using System.Reflection;
using Discord.Interactions;
using DougaBot.Modules.CompressGroup;
using Microsoft.AspNetCore.StaticFiles;
using Serilog;
using Xabe.FFmpeg;
using YoutubeDLSharp.Options;

namespace DougaBot.Services.Video;

public class VideoService : InteractionModuleBase<SocketInteractionContext>, IVideoService
{
    private const int MaxVideoDurationInMinutes = 10;
    private const int MaxVideoDurationInMinutesAfterDownload = 4;

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

    private readonly ILogger _logger;
    private readonly IContentTypeProvider _contentTypeProvider;
    private readonly Globals _globals;

    public VideoService(ILogger logger, IContentTypeProvider contentTypeProvider, Globals globals)
    {
        _logger = logger;
        _contentTypeProvider = contentTypeProvider;
        _globals = globals;
    }

    #endregion

    #region Methods

    public async ValueTask<(string? filePath, string? outPath)> Download(string url, SocketInteractionContext context)
    {
        var fetch = await _globals.FetchAsync(url, TimeSpan.FromMinutes(MaxVideoDurationInMinutes),
            "Could not fetch video data",
        "Could not fetch video data", context.Interaction).ConfigureAwait(false);

        if (fetch is null)
        {
            return default;
        }

        var runDownload = await _globals.DownloadAsync(url,
            "There was an error downloading the video\nPlease try again later",
            new OptionSet
            {
                FormatSort = _globals.FormatSort,
                NoPlaylist = true
            }, context.Interaction).ConfigureAwait(false);

        if (!runDownload)
            return default;

        var id = fetch.ID;
        string path = null!;
        string compressPath = null!;
        var folderUuid = Guid.NewGuid().ToString()[..4];
        var matchingFiles = Directory.GetFiles(_globals.DownloadFolder, $"{id}.*");
        foreach (var matchingFile in matchingFiles)
        {
            if (!_contentTypeProvider.TryGetContentType(matchingFile, out var contentType) ||
                !contentType.StartsWith("video"))
                continue;

            var extension = Path.GetExtension(matchingFile);
            path = matchingFile;
            compressPath = Path.Combine(_globals.DownloadFolder, folderUuid, $"{id}{extension}");
        }

        var mediaInfo = await FFmpeg.GetMediaInfo(path).ConfigureAwait(false);
        var videoStream = mediaInfo.VideoStreams.FirstOrDefault();
        if (videoStream is null)
        {
            File.Delete(path);

            await FollowupAsync("Could not find video stream",
                ephemeral: true,
                options: _globals.ReqOptions).ConfigureAwait(false);

            _logger.Warning("[{Source}] {File} has no video streams found",
                MethodBase.GetCurrentMethod()?.DeclaringType?.Name,
                path);
            return default;
        }
        var duration = videoStream.Duration;

        if (duration == TimeSpan.Zero)
        {
            File.Delete(path);

            await FollowupAsync("Could not find video duration",
                ephemeral: true,
                options: _globals.ReqOptions).ConfigureAwait(false);

            _logger.Warning("[{Source}] {File} has no video duration found",
                MethodBase.GetCurrentMethod()?.DeclaringType?.Name,
                path);
            return default;
        }

        if (duration <= TimeSpan.FromMinutes(MaxVideoDurationInMinutesAfterDownload))
            return (path, compressPath);

        await FollowupAsync(
            $"Video is too long.\nThe video needs to be shorter than {MaxVideoDurationInMinutesAfterDownload} minutes",
            ephemeral: true,
            options: _globals.ReqOptions).ConfigureAwait(false);
        return default;
    }

    public async Task<string?> Compress(string path,
            string outPath,
            CompressGroup.Resolution resEnum,
            SocketInteractionContext context)
    {
        var inputVideoInfo = await FFmpeg.GetMediaInfo(path).ConfigureAwait(false);
        var videoStream = inputVideoInfo.VideoStreams.FirstOrDefault();
        var audioStream = inputVideoInfo.AudioStreams.FirstOrDefault();

        if (videoStream is null)
        {
            File.Delete(path);

            await FollowupAsync("Could not find video stream",
                    ephemeral: true,
                    options: _globals.ReqOptions)
                .ConfigureAwait(false);

            Log.Warning("[{Source}] {File} has no video streams found",
                MethodBase.GetCurrentMethod()?.DeclaringType?.Name,
                path);
            return default;
        }

        if (resEnum is not CompressGroup.Resolution.None)
            await SetRes(videoStream, resEnum).ConfigureAwait(false);

        if (videoStream.Height > 720)
        {
            resEnum = CompressGroup.Resolution.P720;
            await SetRes(videoStream, resEnum).ConfigureAwait(false);
        }

        var conversion = FFmpeg.Conversions.New()
            .AddStream(videoStream)
            .SetPreset(ConversionPreset.VerySlow)
            .SetPixelFormat(PixelFormat.yuv420p)
            .AddParameter("-crf 30");

        if (_iOsCompatible)
        {
            videoStream.SetCodec(VideoCodec.libx264);
            outPath = Path.ChangeExtension(outPath, "mp4");
        }
        else
        {
            videoStream.SetCodec(VideoCodec.vp9);
            conversion.AddParameter(string.Join(" ", _vp9Args));
            outPath = Path.ChangeExtension(outPath, ".webm");
        }
        conversion.SetOutput(outPath);

        if (audioStream is not null)
        {
            conversion.AddStream(audioStream);
            audioStream.SetCodec(!_iOsCompatible
                ? AudioCodec.libopus
                : AudioCodec.aac);
            if (audioStream.Bitrate > 128)
                audioStream.SetBitrate(128);
        }

        _logger.Information("[{Source}] Starting conversion of {File}",
            MethodBase.GetCurrentMethod()?.DeclaringType?.Name,
            path);

        await conversion.Start().ConfigureAwait(false);

        _logger.Information("[{Source}] Finished conversion of {File}",
            MethodBase.GetCurrentMethod()?.DeclaringType?.Name,
            path);

        return outPath;
    }

    public Task SetRes(IVideoStream stream, CompressGroup.Resolution resEnum)
    {
        double originalWidth = stream.Width;
        double originalHeight = stream.Height;

        // Parse the resolution input string (remove the "p" suffix)
        // P144 -> 144
        var resolutionInt = int.Parse(resEnum.ToString()[1..]);

        // Calculate the aspect ratio of the input video
        var aspectRatio = originalWidth / originalHeight;

        // Calculate the output width and height based on the desired resolution and aspect ratio
        var outputWidth = (int)Math.Round(resolutionInt * aspectRatio);
        var outputHeight = resolutionInt;

        // Round the output width and height to even numbers
        outputWidth -= outputWidth % 2;
        outputHeight -= outputHeight % 2;

        stream.SetSize(outputWidth, outputHeight);

        return Task.CompletedTask;
    }

    #endregion
}