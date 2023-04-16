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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

await Globals.CheckForYtdlp().ConfigureAwait(false);
await Globals.CheckForFFmpeg().ConfigureAwait(false);

DiscordSocketConfig socketConfig = new()
{
    GatewayIntents = GatewayIntents.AllUnprivileged &
                     ~GatewayIntents.GuildInvites &
                     ~GatewayIntents.GuildScheduledEvents,
};

var host = Host.CreateDefaultBuilder()
    .ConfigureServices(
        (_, services) =>
        {
            services.AddHttpClient();
            services.AddSingleton(_ => new DiscordSocketClient(socketConfig));
            services.AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()));
            services.AddSingleton<InteractionHandler>();
            services.AddSingleton<Globals>();
            services.AddSingleton<RateLimitService>();
            services.AddTransient<IContentTypeProvider, FileExtensionContentTypeProvider>();
            services.AddTransient<IVideoService, VideoService>();
            services.AddTransient<IAudioService, AudioService>();
            services.AddTransient<ISpeedService, SpeedService>();
            services.AddTransient<ITrimService, TrimService>();
            services.AddSingleton(new AsyncKeyedLocker<string>(o =>
            {
                o.PoolSize = 1;
                o.PoolInitialFill = 1;
                if (Environment.ProcessorCount == 1)
                    return;
                o.PoolSize = Environment.ProcessorCount * 2;
                o.PoolInitialFill = Environment.ProcessorCount * 2;
            }));
        }
    )
    .UseSerilog((hostingContext, _, loggerConfiguration) => loggerConfiguration
        .ReadFrom.Configuration(hostingContext.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
            Path.Combine(Environment.GetEnvironmentVariable("LOG_PATH") ??
                         Path.Combine(Environment.CurrentDirectory, "logs"), "log.txt"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7,
            shared: true,
            flushToDiskInterval: TimeSpan.FromSeconds(1)))
    .Build();

using var serviceScope = host.Services.CreateScope();
var provider = serviceScope.ServiceProvider;
var interactionService = provider.GetRequiredService<InteractionService>();
var socketClient = provider.GetRequiredService<DiscordSocketClient>();
await provider.GetRequiredService<InteractionHandler>().InitializeAsync().ConfigureAwait(false);

var globalTasks = provider.GetRequiredService<Globals>();

socketClient.Log += globalTasks.LogAsync;
interactionService.Log += globalTasks.LogAsync;

// Registers commands globally
if (Convert.ToBoolean(Environment.GetEnvironmentVariable("REGISTER_GLOBAL_COMMANDS")))
    socketClient.Ready += async () => await interactionService.RegisterCommandsGloballyAsync().ConfigureAwait(false);

await socketClient.LoginAsync(TokenType.Bot, Environment.GetEnvironmentVariable("DOUGA_TOKEN")).ConfigureAwait(false);
await socketClient.StartAsync().ConfigureAwait(false);
await Task.Delay(-1).ConfigureAwait(false);