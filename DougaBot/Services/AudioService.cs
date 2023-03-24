using System.Reflection;
using Discord;
using Discord.Interactions;
using DougaBot.PreConditions;
using Serilog;
using Xabe.FFmpeg;
using YoutubeDLSharp.Options;
using static DougaBot.GlobalTasks;

namespace DougaBot.Services;

public class AudioService : InteractionModuleBase<SocketInteractionContext>, IAudioService
{
    #region Constructor

    private readonly GlobalTasks _globalTasks;

    public AudioService(GlobalTasks globalTasks)
    {
        _globalTasks = globalTasks;
    }

    #endregion

    #region Methods
    public async ValueTask<(string? filePath, string? compressPath, SocketInteractionContext? context)>
        DownloadAudioAsync(IAttachment? attachment, string? url, SocketInteractionContext context)
    {
        if (url is null && attachment is null)
        {
            await FollowupAsync("You need to provide either a url or an attachment", ephemeral: true, options: _globalTasks.ReqOptions);
            RateLimitAttribute.ClearRateLimit(context.User.Id);
            return default;
        }
        if (url is not null && attachment is not null)
        {
            await FollowupAsync("You can't provide both a url and an attachment", ephemeral: true, options: _globalTasks.ReqOptions);
            RateLimitAttribute.ClearRateLimit(context.User.Id);
            return default;
        }

        // Fetch audio
        var runFetch = await _globalTasks.RunFetch(url ?? attachment!.Url,
            TimeSpan.FromHours(2),
            "Audio is too long.\nThe audio needs to be shorter than 2 hours",
            "Could not fetch audio",
            context.Interaction);

        if (runFetch is null)
        {
            RateLimitAttribute.ClearRateLimit(context.User.Id);
            return default;
        }

        var folderUuid = Path.GetRandomFileName()[..4];
        var audioPath = Path.Combine(DownloadFolder, $"{runFetch.ID}.m4a");
        var compressedAudioPath = Path.Combine(DownloadFolder, folderUuid, $"{runFetch.ID}.m4a");

        // Download audio
        var runDownload = await _globalTasks.RunDownload(url ?? attachment!.Url,
            "There was an error downloading the audio\nPlease try again later",
            new OptionSet
            {
                NoPlaylist = true,
                AudioFormat = AudioConversionFormat.M4a,
                ExtractAudio = true
            }, context.Interaction);
        if (runDownload)
            return (audioPath, compressedAudioPath, context);

        RateLimitAttribute.ClearRateLimit(context.User.Id);
        return default;
    }

    public async Task<(string filePath, string compressPath, SocketInteractionContext context)>
        CompressAudio(string filePath, string compressPath, int bitrate, SocketInteractionContext context)
    {
        var beforeMediaInfo = await FFmpeg.GetMediaInfo(filePath);
        var audioStreams = beforeMediaInfo.AudioStreams.FirstOrDefault();

        if (audioStreams is null)
        {
            File.Delete(filePath);
            await FollowupAsync("No audio streams found",
                ephemeral: true,
                options: _globalTasks.ReqOptions);
            Log.Warning("[{Source}] {File} has no audio streams found",
                MethodBase.GetCurrentMethod()?.DeclaringType?.Name,
                filePath);
            return default;
        }

        audioStreams.SetBitrate(bitrate);
        audioStreams.SetCodec(AudioCodec.aac);

        await FFmpeg.Conversions.New()
            .AddStream(audioStreams)
            .SetOutput(compressPath)
            .SetPreset(ConversionPreset.VerySlow)
            .Start()
            .ConfigureAwait(false);

        var afterMediaInfo = await FFmpeg.GetMediaInfo(compressPath);

        if (afterMediaInfo.Duration <= TimeSpan.FromHours(2))
            return (filePath, compressPath, context);

        File.Delete(compressPath);
        await FollowupAsync("The Audio needs to be shorter than 2 hours",
            ephemeral: true,
            options: _globalTasks.ReqOptions);
        Log.Warning("[{Source}] {File} is longer than 2 hours",
            MethodBase.GetCurrentMethod()?.DeclaringType?.Name,
            compressPath);
        return default;
    }

    #endregion
}