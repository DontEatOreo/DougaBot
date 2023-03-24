using AsyncKeyedLock;
using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DougaBot;
using DougaBot.Services;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

await GlobalTasks.CheckForYtdlp();
await GlobalTasks.CheckForFFmpeg();

var host = Host.CreateDefaultBuilder()
    .ConfigureServices(
        (_, services) =>
        {
            services.AddSingleton(_ => new DiscordSocketClient(new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.AllUnprivileged & ~GatewayIntents.GuildInvites &
                                 ~GatewayIntents.GuildScheduledEvents,
            }));
            services.AddHttpClient();
            services.AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()));
            services.AddSingleton<InteractionHandler>();
            services.AddSingleton<GlobalTasks>();
            services.AddSingleton<IContentTypeProvider, FileExtensionContentTypeProvider>();
            services.AddSingleton<IVideoService, VideoService>();
            services.AddSingleton<IAudioService, AudioService>();
            services.AddSingleton<ISpeedService, SpeedService>();
            services.AddSingleton<ITrimService, TrimService>();
            services.AddSingleton(new AsyncKeyedLocker<string>(o =>
            {
                o.PoolSize = Environment.ProcessorCount * 4;
                o.PoolInitialFill = 1;
            }));
        }
    )
    .UseSerilog((hostingContext, _, loggerConfiguration) => loggerConfiguration
        .ReadFrom.Configuration(hostingContext.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
            Path.Combine(
                Environment.GetEnvironmentVariable("LOG_PATH") ?? Path.Combine(Environment.CurrentDirectory, "logs"), "log.txt"),
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

socketClient.Log += GlobalTasks.LogAsync;
interactionService.Log += GlobalTasks.LogAsync;

// Registers commands globally
if (Convert.ToBoolean(Environment.GetEnvironmentVariable("REGISTER_GLOBAL_COMMANDS")))
    socketClient.Ready += async () => await interactionService.RegisterCommandsGloballyAsync();

await socketClient.LoginAsync(TokenType.Bot, Environment.GetEnvironmentVariable("DOUGA_TOKEN"));
await socketClient.StartAsync();
await Task.Delay(-1);