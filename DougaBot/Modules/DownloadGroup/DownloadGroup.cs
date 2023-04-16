using Discord.Interactions;
using DougaBot.Services.Audio;
using DougaBot.Services.RateLimit;
using DougaBot.Services.Video;

namespace DougaBot.Modules.DownloadGroup;

[RateLimit, Group("download", "Group of commands for downloading")]
public sealed partial class DownloadGroup : InteractionModuleBase<SocketInteractionContext>
{
    #region Constructor

    private readonly IAudioService _audioService;

    private readonly IVideoService _videoService;

    private readonly Globals _globals;

    public DownloadGroup(IAudioService audioService, IVideoService videoService, Globals globals)
    {
        _audioService = audioService;
        _videoService = videoService;
        _globals = globals;
    }

    #endregion
}