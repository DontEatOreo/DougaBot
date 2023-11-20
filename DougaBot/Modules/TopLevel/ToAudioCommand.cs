using Discord.Interactions;
using DougaBot.Clients;
using JetBrains.Annotations;

namespace DougaBot.Modules.TopLevel;

public sealed partial class TopLevelGroup
{
    [UsedImplicitly]
    [ApiCheck]
    [SlashCommand("toaudio", "Convert a Video to Audio")]
    public async Task ToAudioCommand(Uri uri,
        [Choice("MP3","mp3")]
        [Choice("M4A","aac")]
        string format = "mp3")
    {
        await DeferAsync(options: globals.ReqOptions);

        ToAudioModel model = new() { Uri = uri, Format = format };

        var request = await globals.HandleAsync(model, "toaudio", Context.Guild.PremiumTier);
        switch (request)
        {
            case { ErrorMessage: not null }:
                {
                    await FollowupAsync(request.ErrorMessage, options: globals.ReqOptions);
                    return;
                }
            case { Uri: not null }:
                {
                    var message = $"Your file has been converted to audio!{Environment.NewLine}" +
                                  $"[Click here to download]({request.Uri}){Environment.NewLine}" +
                                  $"The Download Link will expire in {request.Expiry}.";
                    await FollowupAsync(message, options: globals.ReqOptions);
                    return;
                }
        }

        var fileName = request.Headers?.ContentDisposition?.FileName;
        await using var stream = request.ResponseFile;
        if (stream is null)
        {
            await FollowupAsync(request.ErrorMessage, options: globals.ReqOptions);
            return;
        }

        await FollowupWithFileAsync(stream, fileName, options: globals.ReqOptions);
    }
}
