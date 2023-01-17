using System.Diagnostics;
using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Serilog;
using Xabe.FFmpeg;
using static DougaBot.GlobalTasks;

namespace DougaBot.Services;

public class InteractionHandler
{
    private readonly DiscordSocketClient _discordClient;
    private readonly InteractionService _commands;
    private readonly IServiceProvider _services;

    private static readonly bool IosCompatibility = Convert.ToBoolean(Environment.GetEnvironmentVariable("IOS_COMPATIBILITY"));

    private static readonly SemaphoreSlim VideoQueueLock = new(1, 1);

    private static async Task VideoQueueHandler(SocketMessage arg, Attachment attachment)
    {
        await VideoQueueLock.WaitAsync();

        try
        {
            Log.Information("[{Source}] {Message}",
                Assembly.GetExecutingAssembly().GetName().Name,
                $"locked video: {attachment.Filename}");
            await HandleWebM(arg, attachment);
        }
        catch (Exception e)
        {
            Log.Error("[{Source}] {Message}",
                Assembly.GetExecutingAssembly().GetName().Name,
                e.Message);
        }
        finally
        {
            Log.Information("[{Source}] {Message}",
                Assembly.GetExecutingAssembly().GetName().Name,
                $"released video: {attachment.Filename}");
            VideoQueueLock.Release();
        }
    }

    // Using constructor injection
    public InteractionHandler(DiscordSocketClient discordClient, InteractionService commands, IServiceProvider services)
    {
        _discordClient = discordClient;
        _commands = commands;
        _services = services;
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

    private Task HandleMessage(SocketMessage arg)
    {
        // check if it's a bot
        if (arg.Author.IsBot)
            return Task.CompletedTask;

        // Convert VP9 (WebM) videos to H264 (MP4)
        if (!IosCompatibility)
            return Task.CompletedTask;
        foreach (var attachment in arg.Attachments.Where(x => x.Filename.EndsWith(".webm")))
            _ = VideoQueueHandler(arg, attachment);

        return Task.CompletedTask;
    }

    /// <summary>
    ///  This purely exist because iOS doesn't support WebM.
    /// Converts VP9 (Webm) videos to H264 (MP4) automatically in the background.
    /// </summary>
    // ReSharper disable once SuggestBaseTypeForParameter
    private static async Task HandleWebM(SocketMessage arg, Attachment attachment)
    {
        var videoFetch = await YoutubeDl.RunVideoDataFetch(attachment.Url);
        if (!videoFetch.Success)
            return;

        // check if a video is longer than 3 minutes
        if (videoFetch.Data.Duration > TimeSpan.FromMinutes(3).TotalSeconds)
            return;

        await YoutubeDl.RunVideoDownload(attachment.Url);
        var videoId = videoFetch.Data.ID;

        var folderUuid = Guid.NewGuid().ToString()[..4];
        var beforeVideo = Path.Combine(DownloadFolder, $"{videoId}.webm");
        var afterVideo = Path.Combine(DownloadFolder, folderUuid, $"{videoId}.mp4");

        if (File.Exists(afterVideo))
        {
            Log.Information("[{Source}] File already exists: {File}",
                MethodBase.GetCurrentMethod()?.DeclaringType?.Name,
                afterVideo);
            return;
        }

        var mediaInfo = await FFmpeg.GetMediaInfo(beforeVideo);
        var videoStream = mediaInfo.VideoStreams.First();
        var audioStream = mediaInfo.AudioStreams.First();

        if (videoStream is null)
        {
            Log.Warning("[{Source}] [{Message}] NULL Video Stream",
                MethodBase.GetCurrentMethod()
                    ?.DeclaringType?.Name,
                afterVideo);
            return;
        }

        if (videoStream.Duration > TimeSpan.FromMinutes(3))
        {
            Log.Warning("[{Source}] File is too long: {File}",
                MethodBase.GetCurrentMethod()?.DeclaringType?.Name,
                beforeVideo);
            File.Delete(beforeVideo);
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
            .UseMultiThread(Environment.ProcessorCount > 1 ? Environment.ProcessorCount / 2 : 1)
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

        var videoSize = new FileInfo(afterVideo).Length / 1048576f;
        if (videoSize > 100)
        {
            Log.Warning("[{Source}] [{File}] File is too large to embed",
                MethodBase.GetCurrentMethod()?.DeclaringType?.Name,
                afterVideo);
            return;
        }

        var socketGuildUser = (SocketGuildUser)arg.Author;

        switch (socketGuildUser.Guild.PremiumTier)
        {
            case PremiumTier.Tier1 or PremiumTier.None:
                {
                    await UploadWebMVideo(arg, videoSize, 8, afterVideo);
                    break;
                }
            case PremiumTier.Tier2:
                {
                    await UploadWebMVideo(arg, videoSize, 50, afterVideo);
                    return;
                }
            case PremiumTier.Tier3:
                {
                    await UploadWebMVideo(arg, videoSize, 100, afterVideo);
                    break;
                }
            default:
                return;
        }
    }

    private static async Task UploadWebMVideo(SocketMessage arg, float videoSize, int maxSize, string afterVideo)
    {
        if (videoSize <= maxSize)
            await arg.Channel.SendFileAsync(afterVideo,
                messageReference: new MessageReference(arg.Id,
                    failIfNotExists: false));
        else
            File.Delete(afterVideo);
    }

    private static async Task SlashCommandExecuted(SlashCommandInfo arg1, IInteractionContext arg2, IResult arg3)
    {
        if (arg3 is { IsSuccess: false, Error: InteractionCommandError.UnmetPrecondition })
            await arg2.Interaction.RespondAsync(arg3.ErrorReason, ephemeral: true, options: Options);
    }

    private async Task HandleInteraction(SocketInteraction interaction)
    {
        try
        {
            // Create an execution context that matches the generic type parameter of your InteractionModuleBase<T> modules
            var ctx = new SocketInteractionContext(_discordClient, interaction);
            await _commands.ExecuteCommandAsync(ctx, _services);

        }
        catch (Exception ex)
        {
            Log.Error("[{Type}] [{Id}] {Message}", interaction.Type, interaction.Id, ex.Message);

            // If a Slash Command execution fails it is most likely that the original interaction acknowledgement will persist.
            // It is a good idea to delete the original response,
            // or at least let the user know that something went wrong during the command execution.
            if (interaction.Type is InteractionType.ApplicationCommand)
                await interaction.GetOriginalResponseAsync()
                    .ContinueWith(async msg => await msg.Result.DeleteAsync());
        }
    }
}