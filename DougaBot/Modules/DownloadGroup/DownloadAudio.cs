using System.Reflection;
using Discord.Interactions;
using DougaBot.Services.RateLimit;
using JetBrains.Annotations;

namespace DougaBot.Modules.DownloadGroup;

public sealed partial class DownloadGroup
{
    /// <summary>
    /// Download Audio
    /// </summary>
    [UsedImplicitly]
    [RateLimit]
    [SlashCommand("audio", "Download Audio")]
    public async Task Audio(string url)
    {
        await DeferAsync(options: _globals.ReqOptions);
        Uri.TryCreate(url, UriKind.Absolute, out var uri);

        var (downloadPath, _) = await _audioService.Download(uri!, Context);
        if (downloadPath is null)
        {
            RateLimitService.Clear(RateLimitService.RateLimitType.User, Context.User.Id, MethodBase.GetCurrentMethod()!.Name);
            return;
        }

        var fileSize = new FileInfo(downloadPath).Length / _globals.BytesInMegabyte;
        await _globals.UploadAsync(fileSize, downloadPath, Context);
    }
}