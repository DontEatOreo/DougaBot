using Discord.Interactions;
using DougaBot.RateLimit;
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
    public async Task Video(Uri url)
    {
        await DeferAsync(options: globals.ReqOptions);

        DownloadModel model = new() { Uri = url };

        var request = await globals.HandleAsync(model, "download", Context.Guild.PremiumTier);
        switch (request)
        {
            case { ErrorMessage: not null }:
                {
                    await FollowupAsync(request.ErrorMessage, options: globals.ReqOptions);
                    return;
                }
            case { Uri: not null }:
                {
                    var message = $"Your video has been downloaded!{Environment.NewLine}" +
                                  $"[Click here to download]({request.Uri}){Environment.NewLine}" +
                                  $"The Download Link will expire in {request.Expiry}.";
                    await FollowupAsync(message, options: globals.ReqOptions);
                    return;
                }
        }

        var fileName = request.Headers?.ContentDisposition?.FileName;
        await using var stream = request.ResponseFile;

        await FollowupWithFileAsync(stream, fileName, options: globals.ReqOptions);
    }
}