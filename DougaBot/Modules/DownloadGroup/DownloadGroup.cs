using Discord.Interactions;
using DougaBot.RateLimit;

namespace DougaBot.Modules.DownloadGroup;

[RateLimit, Group("download", "Group of commands for downloading")]
public sealed partial class DownloadGroup(Globals globals) : InteractionModuleBase<SocketInteractionContext>;