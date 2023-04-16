using AsyncKeyedLock;
using Discord.Interactions;
using DougaBot.Services.Audio;
using DougaBot.Services.RateLimit;
using DougaBot.Services.Video;
using JetBrains.Annotations;

namespace DougaBot.Modules.CompressGroup;

[RateLimit(60), Group("compress", "Group of commands for compression"), UsedImplicitly]
public sealed partial class CompressGroup : InteractionModuleBase<SocketInteractionContext>
{
    #region Constructor

    private readonly AsyncKeyedLocker<string> _asyncKeyedLocker;
    private readonly IVideoService _videoService;
    private readonly IAudioService _audioService;
    private readonly Globals _globals;

    public CompressGroup(AsyncKeyedLocker<string> asyncKeyedLocker,
        IVideoService videoService,
        IAudioService audioService,
        Globals globals)
    {
        _asyncKeyedLocker = asyncKeyedLocker;
        _videoService = videoService;
        _audioService = audioService;
        _globals = globals;
    }

    #endregion

    private string Key => $"{Context.User.Id.ToString()}";
}