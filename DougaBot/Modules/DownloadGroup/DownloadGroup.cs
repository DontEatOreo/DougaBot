using Discord.Interactions;
using DougaBot.PreConditions;

namespace DougaBot.Modules.DownloadGroup;

[
 RateLimit,
 Group("download", "Group of commands for downloading")
]
public sealed partial class DownloadGroup : InteractionModuleBase<SocketInteractionContext>
{ }