using System.Text.RegularExpressions;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using YoutubeDLSharp;
using YoutubeDLSharp.Metadata;
using YoutubeDLSharp.Options;

namespace DougaBot;

// ReSharper disable once ClassNeverInstantiated.Global
public sealed partial class GlobalTasks
{
    [GeneratedRegex("https?:\\/\\/[^\\s]+")]
    private static partial Regex PartialHttp();

    private static readonly HttpClient HttpClient = new();

    public static readonly RequestOptions Options = new()
    {
        Timeout = 3000,
        RetryMode = RetryMode.AlwaysRetry
    };

    private const string UploadApiLink = "https://litterbox.catbox.moe/resources/internals/api.php";

    public static readonly string DownloadFolder = Path.GetTempPath();

    public static readonly YoutubeDL YoutubeDl = new()
    {
        FFmpegPath = "ffmpeg",
        YoutubeDLPath = "yt-dlp",
        OverwriteFiles = false,
        OutputFileTemplate = "%(id)s.%(ext)s",
        OutputFolder = DownloadFolder
    };

    public const string FormatSort = "res:720";

    public static async Task UploadFile(float fileSize, string filePath, SocketInteractionContext interactionContext)
    {
        if (interactionContext.Guild.PremiumTier is PremiumTier.Tier1 or PremiumTier.None)
        {
            if (fileSize <= 8)
                await interactionContext.Interaction.FollowupWithFileAsync(filePath, options: Options);
        }
        else if (interactionContext.Guild.PremiumTier is PremiumTier.Tier2)
        {
            if (fileSize <= 50)
                await interactionContext.Interaction.FollowupWithFileAsync(filePath, options: Options);
        }
        else if (interactionContext.Guild.PremiumTier is PremiumTier.Tier3)
        {
            if (fileSize <= 100)
                await interactionContext.Interaction.FollowupWithFileAsync(filePath, options: Options);
        }
        else
        {
            await using var fileStream = File.OpenRead(filePath);
            var uploadFileRequest = new MultipartFormDataContent
            {
                { new StringContent("fileupload"), "reqtype" },
                { new StringContent("24h"), "time" },
                { new StreamContent(fileStream), "fileToUpload", filePath }
            };
            var uploadFilePost = await HttpClient.PostAsync(UploadApiLink, uploadFileRequest);
            var fileLink = await uploadFilePost.Content.ReadAsStringAsync();
            await interactionContext.Interaction.FollowupAsync("The download link will **EXPIRE in 24 hours.**",
                options: Options,
                components: new ComponentBuilder().WithButton("Download",
                    style: ButtonStyle.Link,
                    emote: new Emoji("🔗"),
                    url: fileLink)
                    .Build());
        }
    }

    public static async Task<string?> ExtractUrl(string? message, SocketInteraction interaction)
    {
        if (message is null)
            return null;

        var matches = PartialHttp().Matches(message);
        if (matches.Count <= 0)
            return null;
        var urlString = matches.First().Value;

        if (Uri.IsWellFormedUriString(urlString, UriKind.Absolute))
            return urlString;

        await interaction.FollowupAsync("Invalid URL", ephemeral: true, options: Options);
        return null;
    }

    public static async Task<VideoData?> RunDownload(string url,
        TimeSpan durationLimit,
        string durationErrorMessage,
        string dataFetchErrorMessage,
        string downloadErrorMessage,
        OptionSet optionSet, SocketInteraction interaction)
    {
        var runDataFetch = await YoutubeDl.RunVideoDataFetch(url);
        if (!runDataFetch.Success)
        {
            await interaction.FollowupAsync(dataFetchErrorMessage, ephemeral: true, options: Options);
            return null;
        }

        if (runDataFetch.Data.Duration > durationLimit.TotalSeconds)
        {
            await interaction.FollowupAsync(durationErrorMessage, ephemeral: true, options: Options);
            return null;
        }

        var runResult = await YoutubeDl.RunVideoDownload(url, overrideOptions: optionSet);
        if (runResult.Success)
            return runDataFetch.Data;

        await interaction.FollowupAsync(downloadErrorMessage, ephemeral: true, options: Options);
        return null;
    }
}