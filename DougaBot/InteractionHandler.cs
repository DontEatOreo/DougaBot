using System.Reflection;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DougaBot.Modules;
using DougaBot.Modules.CompressGroup;
using Microsoft.Extensions.Options;

namespace DougaBot;

public class InteractionHandler
{
    private const string WebMContentType = "video/webm";

    private readonly Globals _globals;
    private readonly DiscordSocketClient _discordClient;
    private readonly InteractionService _commands;
    private readonly IServiceProvider _services;
    private readonly AppSettings _appSettings;

    public InteractionHandler(Globals globals,
        DiscordSocketClient discordClient,
        InteractionService commands,
        IServiceProvider services,
        IOptions<AppSettings> appSettings)
    {
        _globals = globals;
        _discordClient = discordClient;
        _commands = commands;
        _services = services;
        _appSettings = appSettings.Value;
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
        if (message.Author.IsBot)
            return Task.CompletedTask;

        // Convert VP9 (WebM) videos to H264 (MP4)
        if (_appSettings.AutoConvertWebM is false)
            return Task.CompletedTask;

        IEnumerable<Attachment> attachments = message.Attachments
            .Where(x => x.ContentType == WebMContentType)
            .ToList();

        if (attachments.Any() is false)
            return Task.CompletedTask;
        
        /*
         * We're purposefully using the NON-async state to NOT block the gateway.
         * Essentially, this runs ConvertWebm in the background and responds when it can.
         * We don't care how long it takes, as we don't want to block other more important stuff (such as interactions).
         */
        foreach (var attachment in attachments)
#pragma warning disable CS4014
            ConvertWebm(attachment, message);
#pragma warning restore CS4014

        return Task.CompletedTask;
    }

    private async Task ConvertWebm(IAttachment attachment, SocketMessage message)
    {
        var tier = (message.Channel as IGuildChannel)?.Guild.PremiumTier ?? PremiumTier.None;
        var sizeMiB = attachment.Size / 1024 / 1024; // Formula: bytes (int32) / 1024 / 1024 = MiB
        if (sizeMiB > _globals.MaxSizes[tier])
            return;

        Uri uri = new(attachment.Url);
        CompressModel model = new()
        {
            Uri = uri,
            Resolution = Resolution.P480.ToString(),
            Crf = _appSettings.Crf
        };

        LogMessage webMConvertMessage = new(LogSeverity.Info, nameof(InteractionHandler), $"Converting {attachment.Filename} to MP4");
        await _globals.LogAsync(webMConvertMessage);

        var request = await _globals.HandleAsync(model, "compress", tier);
        if (request.ErrorMessage is not null)
        {
            LogMessage logMessage = new(LogSeverity.Error, nameof(InteractionHandler), request.ErrorMessage);
            await _globals.LogAsync(logMessage);
            return;
        }

        var fileName = request.Headers?.ContentDisposition?.FileName;
        await using var stream = request.ResponseFile;

        MessageReference messageRef = new(message.Id);
        await message.Channel.SendFileAsync(stream, fileName, messageReference: messageRef);
    }

    private async Task SlashCommandExecuted(SlashCommandInfo arg1, IInteractionContext arg2, IResult arg3)
    {
        if (arg3 is { IsSuccess: false, Error: InteractionCommandError.UnmetPrecondition })
            await arg2.Interaction.RespondAsync(arg3.ErrorReason, ephemeral: true, options: _globals.ReqOptions);
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
            LogMessage logMessage = new(LogSeverity.Error, nameof(InteractionHandler), ex.Message, ex);
            await _globals.LogAsync(logMessage);

            // If a Slash Command execution fails it is most likely that the original interaction acknowledgement will persist.
            // It is a good idea to delete the original response,
            // or at least let the user know that something went wrong during the command execution.
            if (interaction.Type is InteractionType.ApplicationCommand)
                await interaction.GetOriginalResponseAsync()
                    .ContinueWith(async msg => await msg.Result.DeleteAsync());
        }
    }
}