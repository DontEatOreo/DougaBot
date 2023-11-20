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
    [RateLimit(15)]
    [SlashCommand("trim", "Trim a video or audio")]
    public async Task TrimCommand(
        [Summary(description: "Format: ss.ms (seconds.milliseconds)")] string startTime,
        [Summary(description: "Format: ss.ms (seconds.milliseconds)")] string endTime,
        IAttachment? attachment = null, Uri? url = null)
    {
        await DeferAsync(options: globals.ReqOptions);

        var uri = attachment?.Url ?? url?.ToString();
        if (uri is null)
        {
            await FollowupAsync("Please provide a video to trim.", options: globals.ReqOptions);
            return;
        }

        var startBool = float.TryParse(startTime, out var start);
        var endBool = float.TryParse(endTime, out var end);

        if (startBool is false || endBool is false)
        {
            await FollowupAsync("Invalid start or end time", options: globals.ReqOptions);
            return;
        }

        if (start <= 0)
        {
            await FollowupAsync("Start time must be greater than 0", options: globals.ReqOptions);
            return;
        }
        if (end <= 0)
        {
            await FollowupAsync("End time must be greater than 0", options: globals.ReqOptions);
            return;
        }

        TrimModel model = new()
        {
            Uri = new Uri(uri),
            Start = start,
            End = end
        };

        var request = await globals.HandleAsync(model, "trim", Context.Guild.PremiumTier);
        switch (request)
        {
            case { ErrorMessage: not null }:
                {
                    await FollowupAsync(request.ErrorMessage, options: globals.ReqOptions);
                    return;
                }
            case { Uri: not null }:
                {
                    var message = $"Your file has been trimmed!{Environment.NewLine}" +
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