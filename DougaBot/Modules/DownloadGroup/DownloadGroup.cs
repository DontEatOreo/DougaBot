using Discord.Interactions;
using DougaBot.PreConditions;

namespace DougaBot.Modules.DownloadGroup;

[RateLimit(15, RateLimitAttribute.RateLimitType.Guild), Group("download", "Group of commands for downloading")]
public sealed partial class DownloadGroup : InteractionModuleBase<SocketInteractionContext>
{
    private static string RemuxVideo => "qt>mp4/mov>mp4/mkv>mp4/webm>mp4/opus>aac";
}