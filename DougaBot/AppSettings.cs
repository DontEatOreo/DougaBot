using System.Text.Json.Serialization;

namespace DougaBot;

public sealed class AppSettings
{
    [JsonPropertyName("ffmpeg_path")]
    public string? FfMpegPath { get; init; }

    [JsonPropertyName("ytdlp_path")]
    public string? YtDlpPath { get; init; }

    [JsonPropertyName("ios_compatability")]
    public bool? IosCompatability { get; init; }

    [JsonPropertyName("crf")]
    public int? Crf { get; init; }

    [JsonPropertyName("register_global_commands")]
    public bool? RegisterGlobalCommands { get; init; }
}