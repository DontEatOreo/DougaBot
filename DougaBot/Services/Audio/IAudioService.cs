using Discord.Interactions;

namespace DougaBot.Services.Audio;

public interface IAudioService
{
    ValueTask<(string? filePath, string? outPath)> Download(string? url, SocketInteractionContext context);

    Task Compress(string filePath, string compressPath, int bitrate);
}