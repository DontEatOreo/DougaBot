using AsyncKeyedLock;
using Discord.Interactions;
using DougaBot.Services;

namespace DougaBot.Modules.TopLevel;

public sealed partial class TopLevelGroup : InteractionModuleBase<SocketInteractionContext>
{
    #region Constructor

    private readonly ISpeedService _speedService;
    private readonly ITrimService _trimService;
    private readonly AsyncKeyedLocker<string> _asyncKeyedLocker;
    private readonly GlobalTasks _globalTasks;

    public TopLevelGroup(AsyncKeyedLocker<string> asyncKeyedLocker,
        ISpeedService speedService,
        ITrimService trimService,
        GlobalTasks globalTasks)
    {
        _asyncKeyedLocker = asyncKeyedLocker;
        _speedService = speedService;
        _trimService = trimService;
        _globalTasks = globalTasks;
    }
    #endregion
}