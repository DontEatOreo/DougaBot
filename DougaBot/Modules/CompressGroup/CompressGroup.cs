using System.Reflection;
using AsyncKeyedLock;
using Discord.Interactions;
using DougaBot.PreConditions;
using Serilog;

namespace DougaBot.Modules.CompressGroup;
[
    RateLimit(60, RateLimitAttribute.RateLimitType.Guild),
    Group("compress", "Group of commands for compression")
]
public sealed partial class CompressGroup : InteractionModuleBase<SocketInteractionContext>
{
    private readonly AsyncKeyedLocker<string> _asyncKeyedLocker;

    private enum CompressionType
    {
        Video,
        Audio
    }

    private struct VideoCompressionParams
    {
        public string? Resolution { get; set; }
        public bool ResolutionChange { get; set; }
    }

    private struct AudioCompressionParams
    {
        public int Bitrate { get; set; }
    }

    public CompressGroup(AsyncKeyedLocker<string> asyncKeyedLocker)
    {
        _asyncKeyedLocker = asyncKeyedLocker;
    }

    private async Task CompressionQueueHandler(string url, string before, string after, CompressionType type, object compressionParams)
    {
        _asyncKeyedLocker.GetOrAdd(Context.Guild.Id.ToString());

        using var loc = await _asyncKeyedLocker.LockAsync(Context.Guild.Id.ToString());
        Log.Information("[{Source}] {Message}",
            MethodBase.GetCurrentMethod()?.DeclaringType?.Name,
                $"{Context.User.Username}#{Context.User.Discriminator} ({Context.User.Id}) locked: {url}");

        switch (type)
        {
            case CompressionType.Video:
                {
                    var videoParams = (VideoCompressionParams)compressionParams;
                    CompressVideo(before, after, videoParams.Resolution, videoParams.ResolutionChange).Start();
                    break;
                }
            case CompressionType.Audio:
                {
                    var audioParams = (AudioCompressionParams)compressionParams;
                    CompressAudio(before, after, audioParams.Bitrate).Start();
                    break;
                }
        }

        Log.Information("[{Source}] {Message}",
            MethodBase.GetCurrentMethod()?.DeclaringType?.Name,
            $"{Context.User.Username}#{Context.User.Discriminator} ({Context.User.Id}) released: {url}");
    }
}