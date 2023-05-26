using Discord.Interactions;

namespace DougaBot.Services.Speed;

public interface ISpeedService
{
    Task<string?> Speed(Uri url, double speed, SocketInteractionContext context);
}