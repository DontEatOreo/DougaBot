using System.Reflection;
using Discord.Interactions;
using DougaBot.PreConditions;
using Serilog;
using Xabe.FFmpeg;
using YoutubeDLSharp.Options;
using static DougaBot.GlobalTasks;

namespace DougaBot.Modules.CompressGroup;

public sealed partial class CompressGroup
{
    private async Task AudioQueueHandler(string url, string before, string after, int bitrate)
    {
        var userLock = QueueLocks.GetOrAdd(Context.User.Id, _ => new SemaphoreSlim(1, 1));

        await userLock.WaitAsync();
        try
        {
            Log.Information("[{Source}] {Message}",
                MethodBase.GetCurrentMethod()?.DeclaringType?.Name,
                $"{Context.User.Username}#{Context.User.Discriminator} ({Context.User.Id}) locked: {url}");
            await CompressAudio(before, after, bitrate);
        }
        catch (Exception e)
        {
            Log.Error("[{Source}] {Message}",
                MethodBase.GetCurrentMethod()?.DeclaringType?.Name,
                e.Message);
        }
        finally
        {
            Log.Information("[{Source}] {Message}",
                MethodBase.GetCurrentMethod()?.DeclaringType?.Name,
                $"{Context.User.Username}#{Context.User.Discriminator} ({Context.User.Id}) released: {url}");
            userLock.Release();
        }
    }

    /// <summary>
    /// Compress Audio
    /// </summary>
    [SlashCommand("audio", "Compress Audio")]
    public async Task SlashCompressAudioCommand(string url,
        [Choice("64k",64),
         Choice("96k",96),
         Choice("128k",128),
         Choice("192k",192),
         Choice("256k",256),
         Choice("320k",320)] int bitrate)
        => await DeferAsync(options: Options)
            .ContinueWith(async _ => await DownloadAudio(url, bitrate));

    private async Task DownloadAudio(string url, int bitrate)
    {
        var runResult = await RunDownload(url,
            TimeSpan.FromHours(2),
            "Audio is too long.\nThe audio needs to be shorter than 2 hours",
            "Could not fetch audio",
            "There was an error downloading the audio\nPlease try again later",
            new OptionSet
            {
                NoPlaylist = true,
                AudioFormat = AudioConversionFormat.M4a,
                ExtractAudio = true
            }, Context.Interaction);

        if (runResult is null)
        {
            RateLimitAttribute.ClearRateLimit(Context.User.Id);
            return;
        }

        var folderUuid = Path.GetRandomFileName()[..4];

        var beforeAudio = Path.Combine(DownloadFolder, $"{runResult.ID}.m4a");
        var afterAudio = Path.Combine(DownloadFolder, folderUuid, $"{runResult.ID}.m4a");

        await AudioQueueHandler(url, beforeAudio, afterAudio, bitrate);
    }

    private async Task CompressAudio(string before,
        string after,
        int bitrate)
    {
        var mediaInfo = await FFmpeg.GetMediaInfo(before);
        var audioStreams = mediaInfo.AudioStreams.First();

        if (audioStreams is null)
        {
            File.Delete(before);
            await FollowupAsync("No audio streams found",
                ephemeral: true,
                options: Options);
            Log.Warning("[{Source}] {File} has no audio streams found",
                MethodBase.GetCurrentMethod()?.DeclaringType?.Name,
                before);
            return;
        }

        audioStreams.SetBitrate(bitrate);
        audioStreams.SetCodec(AudioCodec.aac);

        await FFmpeg.Conversions.New()
            .AddStream(audioStreams)
            .SetOutput(after)
            .SetPreset(ConversionPreset.VerySlow)
            .Start();

        // if a audio is longer than 2 hours delete it
        var duration = FFmpeg.GetMediaInfo(after).Result.Duration;
        if (duration > TimeSpan.FromHours(2))
        {
            File.Delete(after);
            await FollowupAsync("The Audio needs to be shorter than 2 hours",
                ephemeral: true,
                options: Options);
            Log.Warning("[{Source}] {File} is longer than 2 hours",
                MethodBase.GetCurrentMethod()?.DeclaringType?.Name,
                after);
            return;
        }

        var audioSize = new FileInfo(after).Length / 1048576f;

        await UploadFile(audioSize, after, Context);
    }
}