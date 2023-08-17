using Discord;
using Discord.Interactions;
using DougaBot.Clients;
using JetBrains.Annotations;

namespace DougaBot.Modules.CompressGroup;

public sealed partial class CompressGroup
{
    [UsedImplicitly]
    [ApiCheck]
    [SlashCommand("video", "Compress Video")]
    public async Task CompressVideo(IAttachment? attachment = null,
        Uri? url = null,
        Resolution resolution = Resolution.None)
    {
        await DeferAsync(options: _globals.ReqOptions);

        if (attachment is not null && url is not null)
        {
            const string fullInput = "You can only either provide a video or a URL, not both.";
            await FollowupAsync(fullInput, options: _globals.ReqOptions);
            return;
        }

        var uri = attachment?.Url ?? url?.ToString();

        if (uri is null)
        {
            const string emptyInput = "You must provide an attachment or a URL.";
            await FollowupAsync(emptyInput, options: _globals.ReqOptions);
            return;
        }

        CompressModel model = new()
        {
            Uri = new Uri(uri),
            Crf = _appSettings.CurrentValue.Crf
        };
        if (resolution is not Resolution.None)
            model.Resolution = resolution.ToString();

        var request = await _globals.HandleAsync(model, "compress", Context.Guild.PremiumTier);
        switch (request)
        {
            case { ErrorMessage: not null }:
                {
                    await FollowupAsync(request.ErrorMessage, options: _globals.ReqOptions);
                    return;
                }
            case { Uri: not null }:
                {
                    var message = $"Your video has been compressed!{Environment.NewLine}" +
                                  $"[Click here to download]({request.Uri}){Environment.NewLine}" +
                                  $"The Download Link will expire in {request.Expiry}.";
                    await FollowupAsync(message, options: _globals.ReqOptions);
                    return;
                }
        }

        var fileName = request.Headers?.ContentDisposition?.FileName;
        await using var stream = request.ResponseFile;
        if (stream is null)
        {
            await FollowupAsync(request.ErrorMessage, options: _globals.ReqOptions);
            return;
        }

        await FollowupWithFileAsync(stream, fileName, options: _globals.ReqOptions);
    }
}
