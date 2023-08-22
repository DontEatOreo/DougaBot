using System.Collections.Concurrent;
using System.Text.Json;
using Discord;
using DougaBot.Clients;
using Microsoft.Extensions.Options;
using Serilog;
using Serilog.Events;

namespace DougaBot;

public sealed class Globals
{
    private readonly ILogger _logger;
    private readonly ApiClient _apiClient;

    public Globals(ILogger logger, ApiClient apiClient, IOptionsMonitor<AppSettings> appSettings)
    {
        _logger = logger;
        _apiClient = apiClient;
        
        appSettings.OnChange((settings, _) =>
        {
            SetApiStatuses(settings);
        });
    
        SetApiStatuses(appSettings.CurrentValue);
    }
    
    private void SetApiStatuses(AppSettings settings)
    {
        // take a snapshot of the current statuses
        var currentStatuses = ApiStatuses;

        var newStatuses = new ConcurrentDictionary<Uri, bool>();

        foreach (var apiLink in settings.DougaApiLink)
        {
            // preserve the status of existing APIs, default to false for new ones
            var status = currentStatuses
                .TryGetValue(apiLink, out var currStatus) 
                         && currStatus; // by default it's false, unless they're is a match, and all initial statues are false
            
            newStatuses.TryAdd(apiLink, status);
        }

        ApiStatuses = newStatuses;
    }

    /// <summary>
    ///  This is a dictionary of the API links and their statuses.
    ///  true = busy | false = <b>not</b> busy
    /// </summary>
    public ConcurrentDictionary<Uri, bool> ApiStatuses { get; private set; } = new();

    /// <summary>
    /// This is a dictionary of the maximum sizes of the guilds based on their premium tier.
    /// For performance reasons, it's purposefully left to be "mutable", but it shall be treated as immutable.
    /// </summary>
    public readonly Dictionary<PremiumTier, int> MaxSizes = new()
        {
            { PremiumTier.None, 25 },
            { PremiumTier.Tier1, 25 },
            { PremiumTier.Tier2, 50 },
            { PremiumTier.Tier3, 100 }
        };

    public readonly RequestOptions ReqOptions = new()
    {
        Timeout = (int)TimeSpan.FromSeconds(10).TotalMilliseconds,
        RetryMode = RetryMode.AlwaysRetry
    };

    public Task LogAsync(LogMessage message)
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

        _logger.Write(severity, message.Exception,
            "[{Source}] {Message}", message.Source, message.Message);
        return Task.CompletedTask;
    }

    public async Task<ApiResult> HandleAsync<T>(T model, string uriPath, PremiumTier premiumTier)
    {
        var json = JsonSerializer.Serialize(model);

        UriBuilder? uriBuilder = null;

        // The first API link that is not busy will be selected
        var selectedKeyValuePair = ApiStatuses.FirstOrDefault(kv => kv.Value is false);
        var selectedApiLink = selectedKeyValuePair.Key;

        if (selectedApiLink is not null)
        {
            uriBuilder = new UriBuilder
            {
                Scheme = selectedApiLink.Scheme,
                Port = selectedApiLink.Port,
                Host = selectedApiLink.Host,
                Path = uriPath
            };
        }

        if (selectedApiLink is null)
            return new ApiResult { ErrorMessage = $"All APIs are currently busy...{Environment.NewLine}" +
                                                  $"*Please try again later.*" };

        // Mark API link as busy before making the request
        ApiStatuses.TryUpdate(selectedApiLink, true, false);

        ApiResult result;
        try
        {
            result = await _apiClient.DownloadAsync(uriBuilder!.Uri, json);
        }
        finally
        {
            // Mark API link as not busy after getting the response
            ApiStatuses.TryUpdate(selectedApiLink, false, true);
        }

        if (result.ResponseFile is null)
            return new ApiResult { ErrorMessage = result.ErrorMessage };

        /*
         * If the file size is less than or equal to the maximum size of the guild's premium tier,
         * then we return the result as it is.
         * Otherwise, we upload the file to the API and return the result.
         */
        var fileSize = result.ResponseFile.Length;
        
        _ = MaxSizes.TryGetValue(premiumTier, out var maxSize) 
            ? maxSize 
            : MaxSizes[PremiumTier.None];
        
        if (fileSize <= maxSize)
            return new ApiResult
            {
                ResponseFile = result.ResponseFile,
                Headers = result.Headers,
                ErrorMessage = result.ErrorMessage
            };

        var uploadResult = await _apiClient.UploadAsync(result.ResponseFile);
        return new ApiResult
        {
            Uri = uploadResult.Uri,
            Expiry = uploadResult.Expiry,
            Headers = uploadResult.Headers,
            ErrorMessage = uploadResult.ErrorMessage
        };
    }
}