using System.Reflection;
using Discord.Interactions;
using Serilog;
using Xabe.FFmpeg;
using YoutubeDLSharp.Options;

namespace DougaBot.Services.Audio;

public class AudioService : InteractionModuleBase<SocketInteractionContext>, IAudioService
{
    #region Constructor

    private readonly ILogger _logger;
    private readonly Globals _globals;

    public AudioService(Globals globals, ILogger logger)
    {
        _globals = globals;
        _logger = logger;
    }

    #endregion

    #region Methods

    public async ValueTask<(string? filePath, string? outPath)> Download(string? url, SocketInteractionContext context)
    {
        var runFetch = await _globals.FetchAsync(url,
            TimeSpan.FromHours(2),
            "Audio is too long.\nThe audio needs to be shorter than 2 hours",
            "Could not fetch audio",
            context.Interaction).ConfigureAwait(false);

        if (runFetch is null)
            return default;

        var folderUuid = Path.GetRandomFileName()[..4];
        var audioPath = Path.Combine(_globals.DownloadFolder, $"{runFetch.ID}.m4a");
        var compressedAudioPath = Path.Combine(_globals.DownloadFolder, folderUuid, $"{runFetch.ID}.m4a");

        // Download audio
        var runDownload = await _globals.DownloadAsync(url,
            "There was an error downloading the audio\nPlease try again later",
            new OptionSet
            {
                NoPlaylist = true,
                AudioFormat = AudioConversionFormat.M4a,
                ExtractAudio = true
            }, context.Interaction).ConfigureAwait(false);

        return runDownload ? (audioPath, compressedAudioPath) : default;
    }

    public async Task Compress(string filePath, string compressPath, int bitrate)
    {
        var beforeMediaInfo = await FFmpeg.GetMediaInfo(filePath).ConfigureAwait(false);
        var audioStreams = beforeMediaInfo.AudioStreams.FirstOrDefault();

        if (audioStreams is null)
        {
            File.Delete(filePath);
            await FollowupAsync("No audio streams found",
                    ephemeral: true,
                    options: _globals.ReqOptions)
                .ConfigureAwait(false);
            _logger.Warning("[{Source}] {File} has no audio streams found",
                MethodBase.GetCurrentMethod()?.DeclaringType?.Name,
                filePath);
            return;
        }

        audioStreams.SetBitrate(bitrate);
        audioStreams.SetCodec(AudioCodec.aac);

        await FFmpeg.Conversions.New()
            .AddStream(audioStreams)
            .SetOutput(compressPath)
            .SetPreset(ConversionPreset.VerySlow)
            .Start()
            .ConfigureAwait(false);

        var afterMediaInfo = await FFmpeg.GetMediaInfo(compressPath).ConfigureAwait(false);

        if (afterMediaInfo.Duration <= TimeSpan.FromHours(2))
            return;

        File.Delete(compressPath);
        await FollowupAsync("The Audio needs to be shorter than 2 hours",
            ephemeral: true,
            options: _globals.ReqOptions)
            .ConfigureAwait(false);

        _logger.Warning("[{Source}] {File} is longer than 2 hours",
            MethodBase.GetCurrentMethod()?.DeclaringType?.Name,
            compressPath);
    }

    #endregion
}