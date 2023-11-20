using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DougaBot;
using DougaBot.Clients;
using DougaBot.Modules;
using DougaBot.RateLimit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Serilog;

var token = Environment.GetEnvironmentVariable("DOUGA_TOKEN");
if (string.IsNullOrEmpty(token))
    throw new ArgumentNullException(nameof(token),
        "DOUGA_TOKEN environment variable is not set. You must set this to the bot's token.");

DiscordSocketConfig socketConfig = new()
{
    GatewayIntents = GatewayIntents.AllUnprivileged | GatewayIntents.MessageContent |
                     GatewayIntents.GuildMessages & ~GatewayIntents.GuildInvites & ~GatewayIntents.GuildScheduledEvents
};

var host = Host.CreateDefaultBuilder()
    .ConfigureServices(
        (context, services) =>
        {
            var configurationRoot = context.Configuration;

            services.AddHttpClient<ApiClient>();

            services.AddSingleton<AppSettings>();
            services.AddSingleton(_ => new DiscordSocketClient(socketConfig));
            services.AddSingleton(x => new InteractionService(x.GetRequiredService<DiscordSocketClient>()));
            services.AddSingleton<InteractionHandler>();
            services.AddSingleton<Globals>();
            services.AddSingleton<RateLimitService>();

            services.Configure<AppSettings>(configurationRoot.GetSection("DougaSettings"));
        }
    )
    .ConfigureAppConfiguration((hostingContext, config) =>
        {
            config.SetBasePath(hostingContext.HostingEnvironment.ContentRootPath);
            config.AddJsonFile("appsettings.json", false, true);
            config.AddEnvironmentVariables();
        }
    )
    .UseSerilog((hostingContext, _, loggerConfiguration) => loggerConfiguration
        .ReadFrom.Configuration(hostingContext.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File(
            Path.Combine(Path.Combine(Environment.CurrentDirectory, "logs"), "log.txt"),
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: 7,
            shared: true,
            flushToDiskInterval: TimeSpan.FromSeconds(1))
    )
    .Build();

using var serviceScope = host.Services.CreateScope();
var provider = serviceScope.ServiceProvider;
var interactionService = provider.GetRequiredService<InteractionService>();
interactionService.AddGenericTypeConverter<Uri>(typeof(UriConverter<>));
var globals = provider.GetRequiredService<Globals>();
await using var socketClient = provider.GetRequiredService<DiscordSocketClient>();
await provider.GetRequiredService<InteractionHandler>().InitializeAsync();

// get IOptions<AppSettings> from DI
var configuration = provider.GetRequiredService<IOptions<AppSettings>>().Value;

socketClient.Log += globals.LogAsync;
interactionService.Log += globals.LogAsync;

// Registers commands globally
if (configuration.RegisterGlobalCommands)
    socketClient.Ready += async () => await interactionService.RegisterCommandsGloballyAsync();

await socketClient.LoginAsync(TokenType.Bot, Environment.GetEnvironmentVariable("DOUGA_TOKEN"));
await socketClient.StartAsync();
await Task.Delay(-1);