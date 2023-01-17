using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DougaBot.Services;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;
using Microsoft.Extensions.Hosting;

var host = Host.CreateDefaultBuilder()
    .ConfigureServices(
        (_, services) =>
        {
            services.AddSingleton(_ => new DiscordSocketClient(new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.AllUnprivileged & ~GatewayIntents.GuildScheduledEvents & ~GatewayIntents.GuildInvites
            }));
            services.AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()));
            services.AddSingleton<InteractionHandler>();
            services.AddSingleton(_ => new InteractionServiceConfig { LogLevel = LogSeverity.Verbose, UseCompiledLambda = true });
        }
    )
    .UseSerilog((hostingContext, _, loggerConfiguration) => loggerConfiguration
        .ReadFrom.Configuration(hostingContext.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console())
    .Build();

using var serviceScope = host.Services.CreateScope();
var provider = serviceScope.ServiceProvider;
var interactionService = provider.GetRequiredService<InteractionService>();
var socketClient = provider.GetRequiredService<DiscordSocketClient>();
await provider.GetRequiredService<InteractionHandler>().InitializeAsync();

socketClient.Log += LogAsync;
interactionService.Log += LogAsync;

// Registers commands globally
if (Convert.ToBoolean(Environment.GetEnvironmentVariable("REGISER_GLOBAL_COMMANDS")))
    socketClient.Ready += async () => await interactionService.RegisterCommandsGloballyAsync();

await socketClient.LoginAsync(TokenType.Bot, Environment.GetEnvironmentVariable("VIDEO_BOT_TOKEN"));
await socketClient.StartAsync();
await Task.Delay(-1);

static Task LogAsync(LogMessage message)
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