using System.Diagnostics;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DougaBot.Services.Video;
using Serilog;
using Serilog.Events;
using YoutubeDLSharp;
using YoutubeDLSharp.Metadata;
using YoutubeDLSharp.Options;

namespace DougaBot;

public class Globals
{
    #region Constructor

    private readonly CatBoxHttpClient _catBoxHttp;
    private readonly ILogger _logger;
    public readonly YoutubeDL YoutubeDl;

    public Globals(ILogger logger, CatBoxHttpClient catBoxHttp)
    {
        _logger = logger;
        _catBoxHttp = catBoxHttp;
        YoutubeDl = new YoutubeDL
        {
            FFmpegPath = FFmpegPath,
            YoutubeDLPath = YtdlpPath,
            OverwriteFiles = false,
            OutputFileTemplate = "%(id)s.%(ext)s",
            OutputFolder = DownloadFolder
        };
    }

    #endregion

    #region Strings

    private static readonly string FFmpegPath = Environment.GetEnvironmentVariable("FFMPEG_PATH") ?? "ffmpeg";

    private static readonly string YtdlpPath = Environment.GetEnvironmentVariable("YTDLP_PATH") ?? "yt-dlp";

    public readonly string FormatSort = "res:720";

    public readonly string DownloadFolder = Path.GetTempPath();

    public readonly int BytesInMegabyte = 1024 * 1024;

    #endregion

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

    #endregion

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
            await context.Interaction.FollowupWithFileAsync(path, options: ReqOptions).ConfigureAwait(false);
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

    #region Methods

    /// <summary>
    /// Fetches video data and checks if the duration is within the limit.
    /// </summary>
    public async ValueTask<VideoData?> FetchAsync(string? url,
        TimeSpan durationLimit,
        string durationErrorMessage,
        string dataFetchErrorMessage,
        SocketInteraction interaction)
    {
        var runDataFetch = await YoutubeDl.RunVideoDataFetch(url);
        if (!runDataFetch.Success)
        {
            await interaction.FollowupAsync(dataFetchErrorMessage,
                    ephemeral: true,
                    options: ReqOptions)
                .ConfigureAwait(false);
            return null;
        }

        if (!(runDataFetch.Data.Duration > durationLimit.TotalSeconds))
            return runDataFetch.Data;

        await interaction.FollowupAsync(durationErrorMessage,
                ephemeral: true,
                options: ReqOptions)
            .ConfigureAwait(false);
        return null;
    }

    /// <summary>
    /// Downloads a video.
    /// </summary>
    public async ValueTask<bool> DownloadAsync(string? url,
        string downloadErrorMessage,
        OptionSet optionSet,
        SocketInteraction interaction)
    {
        var runResult = await YoutubeDl.RunVideoDownload(url, overrideOptions: optionSet).ConfigureAwait(false);
        if (runResult.Success)
            return true;

        await interaction.FollowupAsync(downloadErrorMessage,
                ephemeral: true,
                options: ReqOptions)
            .ConfigureAwait(false);
        return false;
    }

    #region Checks

    public static Task CheckForFFmpeg()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = FFmpegPath,
                Arguments = "-version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        catch (Exception e)
        {
            Log.Error(e, "FFmpeg not found");
            Environment.Exit(1);
        }

        return Task.CompletedTask;
    }

    public static Task CheckForYtdlp()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = YtdlpPath,
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            });
        }
        catch (Exception e)
        {
            Log.Error(e, "yt-dlp not found");
            Environment.Exit(1);
        }

        return Task.CompletedTask;
    }

    #endregion

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

    #endregion
}