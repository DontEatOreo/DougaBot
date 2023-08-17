using Discord.Interactions;
using DougaBot.RateLimit;

namespace DougaBot.Modules.DownloadGroup;

[RateLimit, Group("download", "Group of commands for downloading")]
public sealed partial class DownloadGroup : InteractionModuleBase<SocketInteractionContext>
{
    private readonly Globals _globals;

    public DownloadGroup(Globals globals)
    {
        _globals = globals;
    }
}