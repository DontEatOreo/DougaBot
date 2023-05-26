using Discord.Interactions;

namespace DougaBot.Services.Trim;

public interface ITrimService
{
    Task<string?> Trim(Uri url, string startTime, string endTime, SocketInteractionContext context);
}