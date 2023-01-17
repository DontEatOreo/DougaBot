using Discord;
using Discord.Interactions;

namespace DougaBot.PreConditions;

/// <summary>
/// A RateLimit Attribute for Slash Commands
/// </summary>
public sealed class RateLimitAttribute : PreconditionAttribute
{
    public enum RateLimitType
    {
        Global,
        User,
        Guild
    }

    private RateLimitType Type { get; }
    private int Seconds { get; }
    private static Dictionary<ulong, DateTime> RateLimits { get; } = new();

    public RateLimitAttribute(int seconds = 5, RateLimitType type = RateLimitType.User)
    {
        Seconds = seconds;
        Type = type;
    }

    public override Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context,
        ICommandInfo commandInfo,
        IServiceProvider services)
    {
        var now = DateTime.UtcNow;

        if (RateLimits.Count > 50)
            RateLimits.Clear();

        switch (Type)
        {
            // Check if the command has been used within the specified time frame
            case RateLimitType.Global when RateLimits.TryGetValue(0, out var value) && (now - value).TotalSeconds < Seconds:
                {
                    var unixTime = FormatRateLimitMessage(now, value, Seconds);
                    return Task.FromResult(PreconditionResult.FromError(unixTime));
                }
            // Update the rate limit information for the global rate limit
            case RateLimitType.Global:
                {
                    RateLimits[0] = now;
                    break;
                }
            case RateLimitType.User:
                {
                    var userId = context.User.Id;

                    // Check if the user has used the command within the specified time frame
                    if (RateLimits.TryGetValue(userId, out var value) && (now - value).TotalSeconds < Seconds)
                    {
                        var unixTime = FormatRateLimitMessage(now, value, Seconds);
                        return Task.FromResult(PreconditionResult.FromError(unixTime));
                    }

                    // Update the rate limit information for the user
                    RateLimits[userId] = now;
                    break;
                }
            case RateLimitType.Guild:
                {
                    var guildId = context.Guild.Id;

                    // Check if the guild has used the command within the specified time frame
                    if (RateLimits.TryGetValue(guildId, out var value) && (now - value).TotalSeconds < Seconds)
                    {
                        var unixTime = FormatRateLimitMessage(now, value, Seconds);
                        return Task.FromResult(PreconditionResult.FromError(unixTime));
                    }

                    // Update the rate limit information for the guild
                    RateLimits[guildId] = now;
                    break;
                }
        }

        return Task.FromResult(PreconditionResult.FromSuccess());
    }

    private static string FormatRateLimitMessage(DateTime now, DateTime value, int seconds)
    {
        // Calculate the remaining time
        var remaining = seconds - (now - value).TotalSeconds;
        // Convert remaining time to unix timestamp
        var unixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (long)remaining;

        // Format the rate limit message with the remaining time
        var message = $"You are being rate limited. Try again <t:{unixTimestamp}:R>";
        return message;
    }

    public static void ClearRateLimit(ulong userId)
        => RateLimits.Remove(userId);
}