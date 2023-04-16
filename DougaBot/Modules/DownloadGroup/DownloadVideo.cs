using System.Reflection;
using Discord.Interactions;
using DougaBot.Services.RateLimit;
using JetBrains.Annotations;

namespace DougaBot.Modules.DownloadGroup;

public sealed partial class DownloadGroup
{
    /// <summary>
    /// Download Video
    /// </summary>
    [UsedImplicitly]
    [RateLimit]
    [SlashCommand("video", "Download Video")]
    public async Task Video(string url)
    {
        await DeferAsync(options: _globals.ReqOptions).ConfigureAwait(false);

        var downloadResult = await _videoService.Download(url, Context).ConfigureAwait(false);
        if (downloadResult.filePath is null || downloadResult.outPath is null)
        {
            RateLimitService.Clear(RateLimitService.RateLimitType.User, Context.User.Id,
                MethodBase.GetCurrentMethod()!.Name);
            return;
        }

        var fileSize = new FileInfo(downloadResult.filePath).Length / _globals.BytesInMegabyte;
        await _globals.UploadAsync(fileSize, downloadResult.filePath, Context).ConfigureAwait(false);
    }
}