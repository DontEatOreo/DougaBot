using Discord.Interactions;

namespace DougaBot.Services.Speed;

public interface ISpeedService
{
    Task<string?> Speed(string? url, double speed, SocketInteractionContext context);
}