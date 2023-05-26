using AsyncKeyedLock;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DougaBot;
using DougaBot.Services;
using DougaBot.Services.Audio;
using DougaBot.Services.RateLimit;
using DougaBot.Services.Speed;
using DougaBot.Services.Trim;
using DougaBot.Services.Video;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;

DiscordSocketConfig socketConfig = new()
{
    GatewayIntents = GatewayIntents.AllUnprivileged &
                     ~GatewayIntents.GuildInvites &
                     ~GatewayIntents.GuildScheduledEvents,
};
AsyncKeyedLocker<string> locker = new(o =>
{
    o.PoolSize = 1;
    o.PoolInitialFill = 1;
    if (Environment.ProcessorCount == 1)
        return;
    o.PoolSize = Environment.ProcessorCount * 2;
    o.PoolInitialFill = Environment.ProcessorCount * 2;
});
DiscordSocketClient client = new(socketConfig);

var logsFolder = Path.Combine(Environment.CurrentDirectory, "logs");
var logPath = Path.Combine(logsFolder, "log.txt");

var host = Host.CreateDefaultBuilder()
    .ConfigureServices(
        (_, services) =>
        {
            services.AddHttpClient<CatBoxClient>();

            services.AddSingleton(locker);
            services.AddSingleton(client);
            services.AddSingleton<InteractionService>();
            services.AddSingleton<InteractionHandler>();
            services.AddSingleton<Globals>();
            services.AddSingleton<RateLimitService>();

            services.AddTransient<IContentTypeProvider, FileExtensionContentTypeProvider>();
            services.AddTransient<IVideoService, VideoService>();
            services.AddTransient<IAudioService, AudioService>();
            services.AddTransient<ISpeedService, SpeedService>();
            services.AddTransient<ITrimService, TrimService>();
        }
    )
    .ConfigureAppConfiguration((hostingContext, config) =>
    {
        config.SetBasePath(hostingContext.HostingEnvironment.ContentRootPath);
        config.AddJsonFile("appsettings.json", true, true);
        config.AddEnvironmentVariables();
    })
    .UseSerilog((context, _, loggerConfiguration) => loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(logPath,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7,
            shared: true,
            flushToDiskInterval: TimeSpan.FromSeconds(1)))
    .Build();

using var serviceScope = host.Services.CreateScope();
var provider = serviceScope.ServiceProvider;
var interactionService = provider.GetRequiredService<InteractionService>();
var socketClient = provider.GetRequiredService<DiscordSocketClient>();
await provider.GetRequiredService<InteractionHandler>().InitializeAsync();

var globalTasks = provider.GetRequiredService<Globals>();
var appSettings = provider.GetRequiredService<IOptions<AppSettings>>();

await globalTasks.CheckForYtdlp();
await globalTasks.CheckForFFmpeg();

socketClient.Log += globalTasks.LogAsync;
interactionService.Log += globalTasks.LogAsync;

// Registers commands globally
if (appSettings.Value.RegisterGlobalCommands ?? false)
    socketClient.Ready += async () => await interactionService.RegisterCommandsGloballyAsync();

var dougaToken = Environment.GetEnvironmentVariable("DOUGA_TOKEN");
await socketClient.LoginAsync(TokenType.Bot, dougaToken);
await socketClient.StartAsync();
await Task.Delay(-1);