using System.Net.NetworkInformation;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;

namespace DougaBot.Clients;

public sealed class ApiCheckAttribute : PreconditionAttribute
{
    public override Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context,
        ICommandInfo commandInfo,
        IServiceProvider services)
    {
        var globals = services.GetRequiredService<Globals>();
        using Ping ping = new();
        var apiLink = globals.ApiStatuses.FirstOrDefault(x => !x.Value).Key;
        if (apiLink is null)
            return Task.FromResult(PreconditionResult.FromError("All APIs are currently busy."));
        var reply = ping.Send(apiLink.Host);

        return Task.FromResult(reply.Status is not IPStatus.Success
            ? PreconditionResult.FromError("The API is currently unavailable.")
            : PreconditionResult.FromSuccess());
    }
}
