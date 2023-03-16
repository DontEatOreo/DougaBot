using System.Text.RegularExpressions;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Serilog;
using Serilog.Events;
using YoutubeDLSharp;
using YoutubeDLSharp.Metadata;
using YoutubeDLSharp.Options;

namespace DougaBot;

public partial class GlobalTasks
{
    [GeneratedRegex("https?:\\/\\/[^\\s]+")]
    private static partial Regex HttpRegex();

    public readonly RequestOptions ReqOptions = new()
    {
        Timeout = 3000,
        RetryMode = RetryMode.AlwaysRetry
    };

    #region Constructor

    private readonly IHttpClientFactory _httpClient;

    public GlobalTasks(IHttpClientFactory httpClient)
    {
        _httpClient = httpClient;
    }

    #endregion

    #region Strings

    private const string UploadApiLink = "https://litterbox.catbox.moe/resources/internals/api.php";

    public static readonly string DownloadFolder = Path.GetTempPath();

    public static readonly Dictionary<PremiumTier, float> MaxFileSizes = new()
    {
        { PremiumTier.Tier1, 8 },
        { PremiumTier.Tier2, 50 },
        { PremiumTier.Tier3, 100 },
        { PremiumTier.None, 8 }
    };

    public static readonly YoutubeDL YoutubeDl = new()
    {
        FFmpegPath = "ffmpeg",
        YoutubeDLPath = "yt-dlp",
        OverwriteFiles = false,
        OutputFileTemplate = "%(id)s.%(ext)s",
        OutputFolder = DownloadFolder
    };

    public const string FormatSort = "res:720";

    #endregion

    #region GlobalMethods

    /// <summary>
    /// Uploads the file either directly (if below maxFileSize) or provides a download link.
    /// </summary>
    public async Task UploadFile(float fileSize, string filePath, SocketInteractionContext interactionContext)
    {
        var maxFileSize = MaxFileSizes[interactionContext.Guild.PremiumTier];
        if (fileSize <= maxFileSize)
            await interactionContext.Interaction.FollowupWithFileAsync(filePath, options: ReqOptions);
        else
        {
            await using var fileStream = File.OpenRead(filePath);
            var uploadFileRequest = new MultipartFormDataContent
            {
                { new StringContent("fileupload"), "reqtype" },
                { new StringContent("24h"), "time" },
                { new StreamContent(fileStream), "fileToUpload", filePath }
            };
            using var client = _httpClient.CreateClient();
            var uploadFilePost = await client.PostAsync(UploadApiLink, uploadFileRequest);
            var fileLink = await uploadFilePost.Content.ReadAsStringAsync();
            await interactionContext.Interaction.FollowupAsync("The download link will **EXPIRE in 24 hours.**",
                options: ReqOptions,
                components: new ComponentBuilder().WithButton("Download",
                        style: ButtonStyle.Link,
                        emote: new Emoji("🔗"),
                        url: fileLink)
                    .Build());
        }
    }

    /// <summary>
    /// Extracts the URL from the given message.
    /// </summary>
    public async Task<string?> ExtractUrl(string? message, SocketInteraction interaction)
    {
        if (message is null)
            return null;

        var matches = HttpRegex().Matches(message);
        if (matches.Count <= 0)
            return null;
        var urlString = matches.First().Value;

        if (Uri.IsWellFormedUriString(urlString, UriKind.Absolute))
            return urlString;

        await interaction.FollowupAsync("Invalid URL", ephemeral: true, options: ReqOptions);
        return null;
    }

    /// <summary>
    /// Fetches video data and checks if the duration is within the limit.
    /// </summary>
    public async Task<VideoData?> RunFetch(string? url,
        TimeSpan durationLimit,
        string durationErrorMessage,
        string dataFetchErrorMessage,
        SocketInteraction interaction)
    {
        var runDataFetch = await YoutubeDl.RunVideoDataFetch(url);
        if (!runDataFetch.Success)
        {
            await interaction.FollowupAsync(dataFetchErrorMessage, ephemeral: true, options: ReqOptions);
            return null;
        }

        if (!(runDataFetch.Data.Duration > durationLimit.TotalSeconds))
            return runDataFetch.Data;

        await interaction.FollowupAsync(durationErrorMessage, ephemeral: true, options: ReqOptions);
        return null;
    }

    public async Task<bool> RunDownload(string? url,
        string downloadErrorMessage,
        OptionSet optionSet,
        SocketInteraction interaction)
    {
        var runResult = await YoutubeDl.RunVideoDownload(url, overrideOptions: optionSet);
        if (runResult.Success)
            return true;

        await interaction.FollowupAsync(downloadErrorMessage, ephemeral: true, options: ReqOptions);
        return false;
    }

    public static Task LogAsync(LogMessage message)
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
        Log.Write(severity, message.Exception, "[{Source}] {Message}", message.Source, message.Message);
        return Task.CompletedTask;
    }

    #endregion
}