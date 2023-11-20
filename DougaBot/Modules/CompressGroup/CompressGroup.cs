using Discord.Interactions;
using DougaBot.RateLimit;
using Microsoft.Extensions.Options;

namespace DougaBot.Modules.CompressGroup;

[RateLimit(60), Group("compress", "Group of commands for compression")]
public sealed partial class CompressGroup(Globals globals, IOptionsMonitor<AppSettings> appSettings)
    : InteractionModuleBase<SocketInteractionContext>;
