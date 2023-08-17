using System.Collections.Concurrent;

namespace DougaBot.RateLimit;

public class RateLimitService
{
    public enum RateLimitType
    {
        Global,
        User,
        Guild
    }

    private static ConcurrentDictionary<(RateLimitType, ulong, string), DateTimeOffset> RateLimits { get; } = new();

    public bool IsRateLimited(RateLimitType type, ulong id, int seconds, string methodName, out string errorMessage)
    {
        var now = DateTimeOffset.UtcNow;
        var key = (type, id, methodName);

        // Remove rate limits that have expired
        foreach (var kvp in RateLimits)
        {
            if ((now - kvp.Value).TotalSeconds >= seconds)
                RateLimits.TryRemove(kvp.Key, out _);
        }

        if (RateLimits.TryGetValue(key, out var value) && (now - value).TotalSeconds < seconds)
        {
            errorMessage = FormatRateLimitMessage(now, value, seconds);
            return true;
        }

        // Update the rate limit information for the key
        RateLimits[key] = now;
        errorMessage = string.Empty;
        return false;
    }

    private string FormatRateLimitMessage(DateTimeOffset now, DateTimeOffset value, int seconds)
    {
        var remaining = seconds - (now - value).TotalSeconds;
        var unixTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + (long)remaining;
        var message = $"You are being rate limited. Try again <t:{unixTimestamp}:R>";
        return message;
    }

    public static void Clear(RateLimitType type, ulong id, string commandName)
        => RateLimits.TryRemove((type, id, commandName), out _);
}