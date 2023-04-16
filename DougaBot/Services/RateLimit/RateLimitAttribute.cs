using System.Runtime.CompilerServices;
using Discord;
using Discord.Interactions;
using Microsoft.Extensions.DependencyInjection;

namespace DougaBot.Services.RateLimit;

public sealed class RateLimitAttribute : PreconditionAttribute
{
    #region Constructor

    private RateLimitService.RateLimitType Type { get; }
    private int Seconds { get; }
    private string MethodName { get; }

    public RateLimitAttribute(int seconds = 5,
        RateLimitService.RateLimitType type = RateLimitService.RateLimitType.User,
        [CallerMemberName] string methodName = "")
    {
        Seconds = seconds;
        Type = type;
        MethodName = methodName;
    }

    #endregion

    public override Task<PreconditionResult> CheckRequirementsAsync(IInteractionContext context,
        ICommandInfo commandInfo,
        IServiceProvider services)
    {
        var rateLimitService = services.GetRequiredService<RateLimitService>();
        var keyId = Type switch
        {
            RateLimitService.RateLimitType.Global => (ulong)0,
            RateLimitService.RateLimitType.User => context.User.Id,
            RateLimitService.RateLimitType.Guild => context.Guild.Id,
            _ => throw new ArgumentOutOfRangeException(nameof(context))
        };

        return Task.FromResult(rateLimitService.IsRateLimited(Type, keyId, Seconds, MethodName, out var errorMessage)
            ? PreconditionResult.FromError(errorMessage)
            : PreconditionResult.FromSuccess());
    }
}