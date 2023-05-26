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
    ValueTask<(string? downloadPath, string? outPath)> Download(Uri url, SocketInteractionContext context);

    Task<string?> Compress(string path,
        string compressPath,
        CompressGroup.Resolution resEnum);

    [UsedImplicitly]
    ValueTask SetRes(IVideoStream stream, CompressGroup.Resolution resEnum);
}