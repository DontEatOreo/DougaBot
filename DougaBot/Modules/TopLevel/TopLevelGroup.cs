using Discord.Interactions;
using JetBrains.Annotations;

namespace DougaBot.Modules.TopLevel;

[UsedImplicitly]
public sealed partial class TopLevelGroup(Globals globals) : InteractionModuleBase<SocketInteractionContext>;