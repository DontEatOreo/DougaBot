using Discord.Interactions;

namespace DougaBot.Services.Audio;

public interface IAudioService
{
    ValueTask<(string? downloadPath, string? compressPath)> Download(Uri url, SocketInteractionContext context);

    Task Compress(string filePath, string compressPath, int bitrate);
}