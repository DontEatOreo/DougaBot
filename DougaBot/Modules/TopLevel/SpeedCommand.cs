using System.ComponentModel.DataAnnotations;
using Discord;
using Discord.Interactions;
using DougaBot.Clients;
using DougaBot.RateLimit;
using JetBrains.Annotations;

namespace DougaBot.Modules.TopLevel;

public sealed partial class TopLevelGroup
{
    [UsedImplicitly]
    [ApiCheck]
    [RateLimit(60)]
    [SlashCommand("speed", "Change the speed of a video/audio")]
    public async Task SpeedCmd([Range(0.25, 4)] double speed, IAttachment? attachment = null, Uri? url = null)
    {
        await DeferAsync(options: _globals.ReqOptions);

        var uri = attachment?.Url ?? url?.ToString();

        if (uri is null)
        {
            const string nullUri = "Please provide a video to speed up.";
            await FollowupAsync(nullUri, options: _globals.ReqOptions);
            return;
        }

        SpeedModel model = new()
        {
            Uri = new Uri(uri),
            Speed = speed
        };

        var request = await _globals.HandleAsync(model, "speed", Context.Guild.PremiumTier);
        switch (request)
        {
            case { ErrorMessage: not null }:
                {
                    await FollowupAsync(request.ErrorMessage, options: _globals.ReqOptions);
                    return;
                }
            case { Uri: not null }:
                {
                    var message = $"Your file speed has been changed!{Environment.NewLine}" +
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