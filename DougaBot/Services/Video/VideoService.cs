using System.Reflection;
using Discord;
using Discord.Interactions;
using DougaBot.Modules.CompressGroup;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Options;
using Xabe.FFmpeg;
using YoutubeDLSharp.Options;

namespace DougaBot.Services.Video;

public class VideoService : InteractionModuleBase<SocketInteractionContext>, IVideoService
{
    #region Constructor

    private readonly IContentTypeProvider _contentTypeProvider;
    private readonly Globals _globals;

    public VideoService(IOptions<AppSettings> appSettings, IContentTypeProvider contentTypeProvider, Globals globals)
    {
        _contentTypeProvider = contentTypeProvider;
        _globals = globals;
        _iOsCompatible = appSettings.Value.IosCompatability ?? true;
        _crf = appSettings.Value.Crf ?? 26;
    }

    #endregion Constructor

    #region Strings

    private readonly TimeSpan _maxVideoDuration = TimeSpan.FromMinutes(10);
    private readonly TimeSpan _maxVideoDurationAfterDownload = TimeSpan.FromMinutes(4);

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

    private readonly bool _iOsCompatible;
    private readonly int _crf;

    private const string DurationErrorMessage = "Could not fetch video data";
    private const string DownloadErrorMessage = "There was an error downloading the video\nPlease try again later";

    #endregion

    #region Methods

    public async ValueTask<(string? downloadPath, string? outPath)> Download(Uri url, SocketInteractionContext context)
    {
        var fetch = await _globals.FetchAsync(url,
            _maxVideoDuration,
            DurationErrorMessage,
            DurationErrorMessage,
            context.Interaction);

        if (fetch is null)
            return default;

        OptionSet optionSet = new()
        {
            FormatSort = _globals.FormatSort,
            NoPlaylist = true
        };
        var runDownload = await _globals.DownloadAsync(url, DownloadErrorMessage, optionSet, context.Interaction);

        if (!runDownload)
            return default;

        var id = fetch.ID;
        string path = null!;
        string compressPath = null!;
        var folderUuid = Guid.NewGuid().ToString()[..4];
        var searchPattern = $"{id}.*";
        var matchingFiles = Directory.GetFiles(Path.GetTempPath(), searchPattern);
        foreach (var matchingFile in matchingFiles)
        {
            _ = !_contentTypeProvider.TryGetContentType(matchingFile, out var contentType);
            if (contentType == null)
                continue;
            if (!contentType.StartsWith("video"))
                continue;

            var extension = Path.GetExtension(matchingFile);
            path = matchingFile;
            compressPath = Path.Combine(Path.GetTempPath(), folderUuid, $"{id}{extension}");
        }

        var mediaInfo = await FFmpeg.GetMediaInfo(path);
        var videoStream = mediaInfo.VideoStreams.FirstOrDefault();
        string? errorMessage;
        if (videoStream is null)
        {
            File.Delete(path);

            errorMessage = "Could not find video stream";
            await FollowupAsync(errorMessage, ephemeral: true, options: _globals.ReqOptions);

            var source = MethodBase.GetCurrentMethod()?.DeclaringType?.Name;
            errorMessage = $"{path} has no video streams found";
            LogMessage logMessage = new(LogSeverity.Warning, source, errorMessage);
            await _globals.LogAsync(logMessage);
            return default;
        }
        var duration = videoStream.Duration;

        if (duration == TimeSpan.Zero)
        {
            File.Delete(path);

            errorMessage = "Invalid video duration";
            await FollowupAsync(errorMessage, ephemeral: true, options: _globals.ReqOptions);

            var source = MethodBase.GetCurrentMethod()?.DeclaringType?.Name;
            errorMessage = $"{path} has no video duration found";
            LogMessage logMessage = new(LogSeverity.Warning, source, errorMessage);
            await _globals.LogAsync(logMessage);
            return default;
        }

        if (duration <= _maxVideoDurationAfterDownload)
            return (path, compressPath);

        errorMessage = $"Video is too long.\nThe video needs to be shorter than {_maxVideoDurationAfterDownload:g} minutes";
        await FollowupAsync(errorMessage, ephemeral: true, options: _globals.ReqOptions);
        return default;
    }

    public async Task<string?> Compress(string path,
        string compressPath,
        CompressGroup.Resolution resEnum)
    {
        var inputVideoInfo = await FFmpeg.GetMediaInfo(path);
        var videoStream = inputVideoInfo.VideoStreams.FirstOrDefault();
        var audioStream = inputVideoInfo.AudioStreams.FirstOrDefault();

        LogMessage logMessage;
        string? source;

        if (videoStream is null)
        {
            File.Delete(path);

            var errorMessage = "Could not find video stream";
            await FollowupAsync(errorMessage, ephemeral: true, options: _globals.ReqOptions);

            source = MethodBase.GetCurrentMethod()?.DeclaringType?.Name;
            errorMessage = $"{path} has no video streams found";
            logMessage = new LogMessage(LogSeverity.Warning, source, errorMessage);
            await _globals.LogAsync(logMessage);
            return default;
        }

        if (resEnum is not CompressGroup.Resolution.None)
            await SetRes(videoStream, resEnum);

        if (videoStream.Height > 720)
        {
            resEnum = CompressGroup.Resolution.P720;
            await SetRes(videoStream, resEnum);
        }

        var conversion = FFmpeg.Conversions.New()
            .AddStream(videoStream)
            .SetPreset(ConversionPreset.VerySlow)
            .SetPixelFormat(PixelFormat.yuv420p)
            .AddParameter($"-crf {_crf}");

        if (_iOsCompatible)
        {
            videoStream.SetCodec(VideoCodec.libx264);
            compressPath = Path.ChangeExtension(compressPath, "mp4");
        }
        else
        {
            videoStream.SetCodec(VideoCodec.vp9);
            conversion.AddParameter(string.Join(" ", _vp9Args));
            compressPath = Path.ChangeExtension(compressPath, ".webm");
        }
        conversion.SetOutput(compressPath);

        if (audioStream is not null)
        {
            conversion.AddStream(audioStream);
            audioStream.SetCodec(!_iOsCompatible
                ? AudioCodec.libopus
                : AudioCodec.aac);
            if (audioStream.Bitrate > 128)
                audioStream.SetBitrate(128);
        }

        source = MethodBase.GetCurrentMethod()?.DeclaringType?.Name;
        var message = $"Starting conversion of {path}";
        logMessage = new LogMessage(LogSeverity.Info, source, message);
        await _globals.LogAsync(logMessage);

        await conversion.Start();

        message = $"Finished conversion of {path}";
        logMessage = new LogMessage(LogSeverity.Info, source, message);
        await _globals.LogAsync(logMessage);

        return compressPath;
    }

    public ValueTask SetRes(IVideoStream stream, CompressGroup.Resolution resEnum)
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

        return ValueTask.CompletedTask;
    }

    #endregion Methods
}