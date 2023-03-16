using Discord.Interactions;
using DougaBot.PreConditions;
using DougaBot.Services;

namespace DougaBot.Modules.DownloadGroup;

[RateLimit, Group("download", "Group of commands for downloading")]
public sealed partial class DownloadGroup : InteractionModuleBase<SocketInteractionContext>
{
    private readonly IAudioService _audioService;

    private readonly IVideoService _videoService;

    private readonly GlobalTasks _globalTasks;

    public DownloadGroup(IAudioService audioService, IVideoService videoService, GlobalTasks globalTasks)
    {
        _audioService = audioService;
        _videoService = videoService;
        _globalTasks = globalTasks;
    }
}