using Discord.Interactions;
using DougaBot.RateLimit;
using Microsoft.Extensions.Options;

namespace DougaBot.Modules.CompressGroup;

[RateLimit(60), Group("compress", "Group of commands for compression")]
public sealed partial class CompressGroup : InteractionModuleBase<SocketInteractionContext>
{
    private readonly Globals _globals;
    private readonly IOptionsMonitor<AppSettings> _appSettings;

    public CompressGroup(Globals globals, IOptionsMonitor<AppSettings> appSettings)
    {
        _globals = globals;
        _appSettings = appSettings;
    }
}
