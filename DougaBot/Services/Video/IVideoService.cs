using Discord.Interactions;
using DougaBot.Modules.CompressGroup;
using JetBrains.Annotations;
using Xabe.FFmpeg;

namespace DougaBot.Services.Video;

public interface IVideoService
{
    /// <summary>
    /// The outPath is the path where ffmpeg will output the compressed video
    /// </summary>
    /// <param name="url"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    ValueTask<(string? filePath, string? outPath)> Download(string url, SocketInteractionContext context);

    [UsedImplicitly]
    Task<string?> Compress(string path,
        string outPath,
        CompressGroup.Resolution resEnum,
        SocketInteractionContext context);

    [UsedImplicitly]
    Task SetRes(IVideoStream stream, CompressGroup.Resolution resEnum);
}