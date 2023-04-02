using AsyncKeyedLock;
using Discord.Interactions;
using DougaBot.PreConditions;
using DougaBot.Services.Audio;
using DougaBot.Services.Video;

namespace DougaBot.Modules.CompressGroup;

[RateLimit(60, RateLimitAttribute.RateLimitType.Guild),
 Group("compress", "Group of commands for compression")]
public sealed partial class CompressGroup : InteractionModuleBase<SocketInteractionContext>
{
    #region Constructor

    private readonly AsyncKeyedLocker<string> _asyncKeyedLocker;
    private readonly IVideoService _videoService;
    private readonly IAudioService _audioService;
    private readonly GlobalTasks _globalTasks;

    public CompressGroup(AsyncKeyedLocker<string> asyncKeyedLocker,
        IVideoService videoService,
        IAudioService audioService,
        GlobalTasks globalTasks)
    {
        _asyncKeyedLocker = asyncKeyedLocker;
        _videoService = videoService;
        _audioService = audioService;
        _globalTasks = globalTasks;
    }

    #endregion

    private string Key => $"{Context.Guild.Id.ToString()}";
}