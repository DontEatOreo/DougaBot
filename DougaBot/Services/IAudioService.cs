using Discord;
using Discord.Interactions;

namespace DougaBot.Services;

public interface IAudioService
{
    ValueTask<(string? filePath, string? compressPath, SocketInteractionContext? context)>
        DownloadAudioAsync(IAttachment? attachment, string? url, SocketInteractionContext context);

    Task<(string filePath, string compressPath, SocketInteractionContext context)>
        CompressAudio(string filePath, string compressPath, int bitrate, SocketInteractionContext context);
}