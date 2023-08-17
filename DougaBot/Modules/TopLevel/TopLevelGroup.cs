using Discord.Interactions;
using JetBrains.Annotations;

namespace DougaBot.Modules.TopLevel;

[UsedImplicitly]
public sealed partial class TopLevelGroup : InteractionModuleBase<SocketInteractionContext>
{
    private readonly Globals _globals;

    public TopLevelGroup(Globals globals)
    {
        _globals = globals;
    }
}