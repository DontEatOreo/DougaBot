using System.Reflection;
using Discord;
using Discord.Interactions;
using Xabe.FFmpeg;
using YoutubeDLSharp.Options;

namespace DougaBot.Services.Audio;

public class AudioService : InteractionModuleBase<SocketInteractionContext>, IAudioService
{
    private readonly Globals _globals;

    public AudioService(Globals globals)
    {
        _globals = globals;
    }

    #region Strings

    private static readonly TimeSpan DurationLimit = TimeSpan.FromHours(2);
    private const string DurationErrorMessage = "Audio is too long.\nThe audio needs to be shorter than 2 hours";
    private const string DataFetchErrorMessage = "Could not fetch audio";
    private const string DownloadErrorMessage = "There was an error downloading the audio\nPlease try again later";

    #endregion Strings

    #region Methods

    public async ValueTask<(string? downloadPath, string? compressPath)> Download(Uri url, SocketInteractionContext context)
    {
        var runFetch = await _globals.FetchAsync(url,
            DurationLimit,
            DurationErrorMessage,
            DataFetchErrorMessage,
            context.Interaction);

        if (runFetch is null)
            return default;

        var folderUuid = Path.GetRandomFileName()[..4];
        var fileName = $"{runFetch.ID}.m4a";
        var audioPath = Path.Combine(Path.GetTempPath(), fileName);
        var compressedAudioPath = Path.Combine(Path.GetTempPath(), folderUuid, fileName);

        OptionSet optionSet = new()
        {
            NoPlaylist = true,
            AudioFormat = AudioConversionFormat.M4a,
            ExtractAudio = true
        };

        // Download audio
        var runDownload = await _globals.DownloadAsync(url, DownloadErrorMessage, optionSet, context.Interaction);

        return runDownload ? (audioPath, compressedAudioPath) : default;
    }

    public async Task Compress(string filePath, string compressPath, int bitrate)
    {
        var beforeMediaInfo = await FFmpeg.GetMediaInfo(filePath);
        var audioStreams = beforeMediaInfo.AudioStreams.FirstOrDefault();

        string? errorMessage;
        LogMessage logMessage;
        string? source;

        if (audioStreams is null)
        {
            File.Delete(filePath);

            errorMessage = "No audio streams found";
            await FollowupAsync(errorMessage, ephemeral: true, options: _globals.ReqOptions);

            source = MethodBase.GetCurrentMethod()?.DeclaringType?.Name;
            errorMessage = $"{filePath} has no audio streams found";
            logMessage = new LogMessage(LogSeverity.Warning, source, errorMessage);
            await _globals.LogAsync(logMessage);
            return;
        }

        audioStreams.SetBitrate(bitrate);
        audioStreams.SetCodec(AudioCodec.aac);

        await FFmpeg.Conversions.New()
            .AddStream(audioStreams)
            .SetOutput(compressPath)
            .SetPreset(ConversionPreset.VerySlow)
            .Start();

        var afterMediaInfo = await FFmpeg.GetMediaInfo(compressPath);

        if (afterMediaInfo.Duration <= DurationLimit)
            return;

        File.Delete(compressPath);
        await FollowupAsync(DurationErrorMessage, ephemeral: true, options: _globals.ReqOptions);

        source = MethodBase.GetCurrentMethod()?.DeclaringType?.Name;
        errorMessage = $"{compressPath} is longer than {DurationLimit:g}";
        logMessage = new LogMessage(LogSeverity.Warning, source, errorMessage);
        await _globals.LogAsync(logMessage);
    }

    #endregion
}