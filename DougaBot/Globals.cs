using System.Diagnostics;
using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DougaBot.Services.Video;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;
using YoutubeDLSharp;
using YoutubeDLSharp.Metadata;
using YoutubeDLSharp.Options;

namespace DougaBot;

public class Globals
{
    #region Constructor

    private readonly CatBoxClient _catBoxHttp;
    private readonly ILogger _logger;
    public readonly YoutubeDL YoutubeDl;

    public Globals(ILogger logger, CatBoxClient catBoxHttp, IOptions<AppSettings> appSettings)
    {
        _fFmpegPath = appSettings.Value.FfMpegPath ?? "ffmpeg";
        _ytdlpPath = appSettings.Value.YtDlpPath ?? "yt-dlp";

        _logger = logger;
        _catBoxHttp = catBoxHttp;
        YoutubeDl = new YoutubeDL
        {
            FFmpegPath = _fFmpegPath,
            YoutubeDLPath = _ytdlpPath,
            OverwriteFiles = false,
            OutputFileTemplate = "%(id)s.%(ext)s",
            OutputFolder = Path.GetTempPath()
        };
    }

    #endregion Constructor

    #region Strings

    private static string? _fFmpegPath;
    private static string? _ytdlpPath;
    public readonly string FormatSort = "res:720,ext:mp4";
    public readonly int BytesInMegabyte = 1024 * 1024;

    #endregion Strings

    #region Fields

    public readonly RequestOptions ReqOptions = new()
    {
        Timeout = 10_000,
        RetryMode = RetryMode.AlwaysRetry
    };

    /// <summary>
    /// Max file size in MB for each premium tier.
    /// </summary>
    public readonly Dictionary<PremiumTier, float> MaxSizes = new()
    {
        { PremiumTier.Tier1, 25 },
        { PremiumTier.Tier2, 50 },
        { PremiumTier.Tier3, 100 },
        { PremiumTier.None, 25 }
    };

    #endregion Fields

    #region Methods

    /// <summary>
    /// Uploads a file to the server or an api
    /// </summary>
    /// <param name="size">Max size in MiB.</param>
    /// <param name="path">Path to the file.</param>
    /// <param name="context">Interaction context.</param>
    public async ValueTask UploadAsync(float size, string path, SocketInteractionContext context)
    {
        var maxSize = MaxSizes[context.Guild.PremiumTier];
        if (size <= maxSize)
        {
            await context.Interaction.FollowupWithFileAsync(path, options: ReqOptions);
            return;
        }
        var fileLink = await _catBoxHttp.UploadFile(path);
        var component = new ComponentBuilder().WithButton("Download",
                style: ButtonStyle.Link,
                emote: new Emoji("🔗"),
                url: fileLink)
            .Build();
        await context.Interaction.FollowupAsync("The download link will **EXPIRE in 24 hours.**",
            options: ReqOptions,
            components: component);
    }

    /// <summary>
    /// Fetches video data and checks if the duration is within the limit.
    /// </summary>
    public async ValueTask<VideoData?> FetchAsync(Uri url,
        TimeSpan durationLimit,
        string durationErrorMessage,
        string dataFetchErrorMessage,
        SocketInteraction interaction)
    {
        var runDataFetch = await YoutubeDl.RunVideoDataFetch(url.ToString());
        if (!runDataFetch.Success)
        {
            await interaction.FollowupAsync(dataFetchErrorMessage,
                    ephemeral: true,
                    options: ReqOptions);
            return null;
        }

        if (!(runDataFetch.Data.Duration > durationLimit.TotalSeconds))
            return runDataFetch.Data;

        await interaction.FollowupAsync(durationErrorMessage,
                ephemeral: true,
                options: ReqOptions);
        return null;
    }

    /// <summary>
    /// Downloads a video.
    /// </summary>
    public async ValueTask<bool> DownloadAsync(Uri url,
        string downloadErrorMessage,
        OptionSet optionSet,
        SocketInteraction interaction)
    {
        var runResult = await YoutubeDl.RunVideoDownload(url.ToString(), overrideOptions: optionSet);
        if (runResult.Success)
            return true;

        await interaction.FollowupAsync(downloadErrorMessage, ephemeral: true, options: ReqOptions);
        return false;
    }

    #endregion Methods

    #region Checks

    public async Task CheckForFFmpeg()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _fFmpegPath,
                Arguments = "-version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        catch (Exception e)
        {
            var source = MethodBase.GetCurrentMethod()?.DeclaringType?.FullName;
            const string message = "FFmpeg not found";
            LogMessage logMessage = new(LogSeverity.Critical, source, message, e);
            await LogAsync(logMessage);
            Environment.Exit(1);
        }
    }

    public async Task CheckForYtdlp()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = _ytdlpPath,
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        catch (Exception e)
        {
            var source = MethodBase.GetCurrentMethod()?.DeclaringType?.FullName;
            const string message = "YtDlp not found";
            LogMessage logMessage = new(LogSeverity.Critical, source, message, e);
            await LogAsync(logMessage);
            Environment.Exit(1);
        }
    }

    public Task LogAsync(LogMessage message)
    {
        var severity = message.Severity switch
        {
            LogSeverity.Critical => LogEventLevel.Fatal,
            LogSeverity.Error => LogEventLevel.Error,
            LogSeverity.Warning => LogEventLevel.Warning,
            LogSeverity.Info => LogEventLevel.Information,
            LogSeverity.Verbose => LogEventLevel.Verbose,
            LogSeverity.Debug => LogEventLevel.Debug,
            _ => LogEventLevel.Information
        };
        _logger.Write(severity, message.Exception, "[{Source}] {Message}", message.Source, message.Message);
        return Task.CompletedTask;
    }

    #endregion Methods
}