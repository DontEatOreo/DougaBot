using System.Text.Json.Serialization;

namespace DougaBot.Modules.DownloadGroup;

public class DownloadModel
{
    [JsonPropertyName("uri")]
    public Uri? Uri { get; init; }
}
