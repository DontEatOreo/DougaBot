using Discord.Interactions;

namespace DougaBot.Services;

public interface ITrimService
{
    Task<string?> TrimTaskAsync(string url, string startTime, string endTime, SocketInteractionContext context);
}