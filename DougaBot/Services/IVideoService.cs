using Discord;
using Discord.Interactions;
using Xabe.FFmpeg;

namespace DougaBot.Services;

public interface IVideoService
{
    ValueTask<(string? filePath, string? compressPath, string? resolution, bool resolutionChange, SocketInteractionContext? context)>
        DownloadVideoAsync(IAttachment? attachment, string? url, string? resolution, SocketInteractionContext context);
    Task<(string videoPath, string outputPath, SocketInteractionContext context)>
        CompressVideoAsync(string videoPath, string outputPath, string? resolution, bool resolutionChange, SocketInteractionContext context);

    ValueTask SetResolutionAsync(IVideoStream videoStream, int inputWidth, int inputHeight);
    ValueTask CalculateResolutionAsync(IVideoStream videoStream, string resolution);
}