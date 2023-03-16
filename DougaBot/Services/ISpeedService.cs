using Discord.Interactions;

namespace DougaBot.Services;

public interface ISpeedService
{
    Task<string?> SpeedTaskAsync(string url, double speed, SocketInteractionContext context);
}