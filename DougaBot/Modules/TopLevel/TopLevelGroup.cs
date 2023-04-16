using AsyncKeyedLock;
using Discord.Interactions;
using DougaBot.Services.Speed;
using DougaBot.Services.Trim;
using JetBrains.Annotations;

namespace DougaBot.Modules.TopLevel;

[UsedImplicitly]
public sealed partial class TopLevelGroup : InteractionModuleBase<SocketInteractionContext>
{
    #region Constructor

    private readonly ISpeedService _speedService;
    private readonly ITrimService _trimService;
    private readonly AsyncKeyedLocker<string> _asyncKeyedLocker;
    private readonly Globals _globals;

    public TopLevelGroup(AsyncKeyedLocker<string> asyncKeyedLocker,
        ISpeedService speedService,
        ITrimService trimService,
        Globals globals)
    {
        _asyncKeyedLocker = asyncKeyedLocker;
        _speedService = speedService;
        _trimService = trimService;
        _globals = globals;
    }
    #endregion

    private string Key => $"{Context.User.Id.ToString()}";
}