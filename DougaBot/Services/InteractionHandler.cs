using System.Diagnostics;
using System.Reflection;
using AsyncKeyedLock;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DougaBot.Modules.CompressGroup;
using Microsoft.Extensions.Options;
using Xabe.FFmpeg;

namespace DougaBot.Services;

public class InteractionHandler
{
    #region Constructor
    private readonly DiscordSocketClient _discordClient;
    private readonly InteractionService _commands;
    private readonly IServiceProvider _services;
    private readonly AsyncKeyedLocker<string> _asyncKeyedLocker;
    private readonly Globals _globals;

    public InteractionHandler(DiscordSocketClient discordClient,
        InteractionService commands,
        IServiceProvider services,
        AsyncKeyedLocker<string> asyncKeyedLocker,
        Globals globals, IOptions<AppSettings> appSettings)
    {
        _iosCompatibility = appSettings.Value.IosCompatability ?? true;

        _discordClient = discordClient;
        _commands = commands;
        _services = services;
        _asyncKeyedLocker = asyncKeyedLocker;
        _globals = globals;
    }

    #endregion

    #region Fields

    private static bool _iosCompatibility;

    private const string WebMQueueKey = "WebMQueueKey";

    #endregion

    #region Methods

    private async Task VideoQueueHandler(SocketMessage message, List<Attachment> attachments)
    {
        using var _ = await _asyncKeyedLocker.LockAsync(WebMQueueKey);
        foreach (var attachment in attachments)
        {
            LogMessage logMessage;
            var loggingMessage = $"{message.Author.Username}#{message.Author.Discriminator} has locked: {attachment.Url}";
            var source = MethodBase.GetCurrentMethod()?.DeclaringType?.Name;

            logMessage = new LogMessage(LogSeverity.Info, source, loggingMessage);
            await _globals.LogAsync(logMessage);

            await HandleWebM(message, attachment);

            loggingMessage = $"{message.Author.Username}#{message.Author.Discriminator} has unlocked: {attachment.Url}";
            logMessage = new LogMessage(LogSeverity.Info, source, loggingMessage);
            await _globals.LogAsync(logMessage);
        }
    }

    public async Task InitializeAsync()
    {
        // Add the public modules that inherit InteractionModuleBase<T> to the InteractionService
        await _commands.AddModulesAsync(Assembly.GetEntryAssembly(), _services);

        // Process the InteractionCreated payloads to execute Interactions commands
        _discordClient.InteractionCreated += HandleInteraction;
        _discordClient.MessageReceived += HandleMessage;

        // Process the command execution results
        _commands.SlashCommandExecuted += SlashCommandExecuted;
    }

    private Task HandleMessage(SocketMessage message)
    {
        // check if it's a bot
        if (message.Author.IsBot)
            return Task.CompletedTask;

        // Convert VP9 (WebM) videos to H264 (MP4)
        if (!_iosCompatibility)
            return Task.CompletedTask;

        var attachments = message.Attachments
            .Where(x => x.ContentType is "video/webm")
            .ToList();

        if (attachments.Count > 0)
            _ = VideoQueueHandler(message, attachments);

        return Task.CompletedTask;
    }

    /// <summary>
    ///  This purely exist because iOS doesn't support WebM.
    /// Converts VP9 (Webm) videos to H264 (MP4) automatically in the background.
    /// </summary>
    // ReSharper disable once SuggestBaseTypeForParameter
    private async Task HandleWebM(SocketMessage message, Attachment attachment)
    {
        var videoFetch = await _globals.YoutubeDl.RunVideoDataFetch(attachment.Url);
        if (!videoFetch.Success)
            return;

        // check if a video is longer than 3 minutes
        if (videoFetch.Data.Duration > TimeSpan.FromMinutes(3).TotalSeconds)
            return;

        var download = await _globals.YoutubeDl.RunVideoDownload(attachment.Url);
        if (!download.Success)
            return;

        var videoId = videoFetch.Data.ID;

        var folderUuid = Guid.NewGuid().ToString()[..4];
        var beforeVideo = Path.Combine(Path.GetTempPath(), $"{videoId}.webm");
        var afterVideo = Path.Combine(Path.GetTempPath(), folderUuid, $"{videoId}.mp4");

        if (File.Exists(afterVideo))
        {
            LogMessage logMessage = new(LogSeverity.Warning, "Video", "File already exists");
            await _globals.LogAsync(logMessage);
            return;
        }

        var mediaInfo = await FFmpeg.GetMediaInfo(beforeVideo);
        var videoStream = mediaInfo.VideoStreams.FirstOrDefault();
        var audioStream = mediaInfo.AudioStreams.FirstOrDefault();

        if (videoStream is null)
        {
            LogMessage logMessage = new(LogSeverity.Warning, "Video", "NULL Video Stream");
            await _globals.LogAsync(logMessage);
            return;
        }

        if (videoStream.Duration > TimeSpan.FromMinutes(3))
        {
            LogMessage logMessage = new(LogSeverity.Warning, "Video", "File is too long");
            await _globals.LogAsync(logMessage);
            return;
        }

        videoStream.SetCodec(VideoCodec.h264);

        // Compress Video
        var conversion = FFmpeg.Conversions.New()
            .AddStream(videoStream)
            .SetOutput(afterVideo)
            .SetPreset(ConversionPreset.VerySlow)
            .SetPixelFormat(PixelFormat.yuv420p)
            .SetPriority(ProcessPriorityClass.BelowNormal)
            .AddParameter("-crf 30");

        if (audioStream is not null)
        {
            conversion.AddStream(audioStream);
            audioStream.SetCodec(AudioCodec.aac);
            if (audioStream.Bitrate > 128)
                audioStream.SetBitrate(128);
        }

        await conversion.Start();

        var videoSize = new FileInfo(afterVideo).Length / _globals.BytesInMegabyte;
        if (videoSize > 100)
        {
            LogMessage logMessage = new(LogSeverity.Warning, "Video", "File is too large to embed");
            await _globals.LogAsync(logMessage);
            File.Delete(afterVideo);
            return;
        }

        var socketGuildUser = (SocketGuildUser)message.Author;
        var maxFileSize = _globals.MaxSizes[socketGuildUser.Guild.PremiumTier];
        if (videoSize <= maxFileSize)
        {
            await message.Channel.SendFileAsync(afterVideo,
                messageReference: new MessageReference(message.Id,
                    failIfNotExists: false));
        }
    }

    private async Task SlashCommandExecuted(SlashCommandInfo arg1, IInteractionContext arg2, IResult arg3)
    {
        if (arg3 is { IsSuccess: false, Error: InteractionCommandError.UnmetPrecondition })
            await arg2.Interaction.RespondAsync(arg3.ErrorReason,
                    ephemeral: true,
                    options: _globals.ReqOptions);
    }

    private async Task HandleInteraction(SocketInteraction interaction)
    {
        try
        {
            // Create an execution context that matches the generic type parameter of your InteractionModuleBase<T> modules
            SocketInteractionContext ctx = new(_discordClient, interaction);
            await _commands.ExecuteCommandAsync(ctx, _services);
        }
        catch (Exception ex)
        {
            LogMessage logMessage = new(LogSeverity.Error, "Interaction", ex.Message);
            await _globals.LogAsync(logMessage);

            // If a Slash Command execution fails it is most likely that the original interaction acknowledgement will persist.
            // It is a good idea to delete the original response,
            // or at least let the user know that something went wrong during the command execution.
            if (interaction.Type is InteractionType.ApplicationCommand)
                await interaction.GetOriginalResponseAsync()
                    .ContinueWith(async msg => await msg.Result.DeleteAsync());
        }
    }

    #endregion
}